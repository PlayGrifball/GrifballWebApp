#!/bin/bash
# Reconciles a clusterless (CLUSTER_TYPE = NONE) availability group over the StatefulSet's SQL Servers.
set -u
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
PW_SQL=${SA_PASSWORD//\'/\'\'}
q() {
  local out rc
  out=$("$SQLCMD" -C -S "$1" -U sa -P "$SA_PASSWORD" -y 0 -b -l 10 -Q "SET NOCOUNT ON; $2" 2>&1); rc=$?
  if [[ $rc -ne 0 ]]; then echo "$out" >&2; return $rc; fi
  # -y 0 keeps long values whole (and prints no header); -h -1 can't go with it.
  printf '%s\n' "$out" | sed 's/[[:space:]]*$//; s/^[[:space:]]*//' | sed '/^$/d'
}
host() { echo "$STS-$1.$SVC.$NAMESPACE.svc.cluster.local"; }
pods() { seq 0 $((REPLICAS - 1)); }

label() {
  wget -q -O /dev/null --ca-certificate=/var/run/secrets/kubernetes.io/serviceaccount/ca.crt \
    --header="Authorization: Bearer $(cat /var/run/secrets/kubernetes.io/serviceaccount/token)" \
    --header="Content-Type: application/merge-patch+json" --method=PATCH --max-redirect=0 \
    --body-data="{\"metadata\":{\"labels\":{\"$LABEL\":\"$2\"}}}" \
    "https://kubernetes.default.svc/api/v1/namespaces/$NAMESPACE/pods/$1"
}

cert_hex() { q "$1" "SELECT CONVERT(varchar(max), CERTENCODED(CERT_ID('ag_cert')), 1)"; }
key_hex() { q "$1" "SELECT CONVERT(varchar(max), CERTPRIVATEKEY(CERT_ID('ag_cert'), '$PW_SQL'), 1)"; }

reconcile() {
  local i h db up=()
  for i in $(pods); do
    h=$(host "$i")
    if q "$h" "SELECT 1" >/dev/null; then up+=("$i"); fi
  done
  [[ ${#up[@]} -eq 0 ]] && { echo "no SQL Server is up"; return; }

  # Certificate: made once, then copied to every replica; it authenticates the mirroring endpoints.
  local src="" have
  for i in "${up[@]}"; do
    [[ "$(q "$(host "$i")" "SELECT COUNT(*) FROM sys.certificates WHERE name = 'ag_cert'")" = 1 ]] && { src=$i; break; }
  done
  if [[ -z "$src" ]]; then
    [[ "${up[0]}" = 0 ]] || { echo "waiting for $STS-0 to make the certificate"; return; }
    q "$(host 0)" "IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##') CREATE MASTER KEY ENCRYPTION BY PASSWORD = '$PW_SQL'; CREATE CERTIFICATE ag_cert WITH SUBJECT = 'availability group', EXPIRY_DATE = '2099-01-01'" || return
    src=0
  fi
  local cert key
  for i in "${up[@]}"; do
    h=$(host "$i")
    have=$(q "$h" "SELECT COUNT(*) FROM sys.certificates WHERE name = 'ag_cert'")
    if [[ "$have" != 1 ]]; then
      cert=$(cert_hex "$(host "$src")"); key=$(key_hex "$(host "$src")")
      q "$h" "IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##') CREATE MASTER KEY ENCRYPTION BY PASSWORD = '$PW_SQL'; CREATE CERTIFICATE ag_cert FROM BINARY = $cert WITH PRIVATE KEY (BINARY = $key, DECRYPTION BY PASSWORD = '$PW_SQL')" || return
    fi
    q "$h" "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'ag_login') CREATE LOGIN ag_login FROM CERTIFICATE ag_cert;
IF NOT EXISTS (SELECT 1 FROM sys.database_mirroring_endpoints) CREATE ENDPOINT Hadr_endpoint STATE = STARTED AS TCP (LISTENER_PORT = 5022) FOR DATABASE_MIRRORING (ROLE = ALL, AUTHENTICATION = CERTIFICATE ag_cert, ENCRYPTION = REQUIRED ALGORITHM AES);
GRANT CONNECT ON ENDPOINT::Hadr_endpoint TO ag_login" || return
  done

  # The group: created on the first replica, joined by the others.
  local replicas="" exists=""
  for i in $(pods); do
    replicas="$replicas${replicas:+, }N'$STS-$i' WITH (ENDPOINT_URL = N'TCP://$(host "$i"):5022', AVAILABILITY_MODE = $MODE, FAILOVER_MODE = MANUAL, SEEDING_MODE = AUTOMATIC, SECONDARY_ROLE (ALLOW_CONNECTIONS = ALL))"
  done
  for i in "${up[@]}"; do
    [[ "$(q "$(host "$i")" "SELECT COUNT(*) FROM sys.availability_groups WHERE name = '$AG'")" = 1 ]] && exists=1
  done
  if [[ -z "$exists" ]]; then
    [[ "${up[0]}" = 0 ]] || return
    q "$(host 0)" "CREATE AVAILABILITY GROUP [$AG] WITH (CLUSTER_TYPE = NONE) FOR REPLICA ON $replicas" || return
  fi
  for i in "${up[@]}"; do
    h=$(host "$i")
    if [[ "$(q "$h" "SELECT COUNT(*) FROM sys.availability_groups WHERE name = '$AG'")" != 1 ]]; then
      q "$h" "ALTER AVAILABILITY GROUP [$AG] JOIN WITH (CLUSTER_TYPE = NONE)" || continue
    fi
    q "$h" "ALTER AVAILABILITY GROUP [$AG] GRANT CREATE ANY DATABASE" >/dev/null
  done

  # Roles, and the label the Services select on. Two replicas claiming PRIMARY (a forced failover while the old
  # primary was up) is a split brain: nothing is labelled primary until one is demoted.
  local primary="" role primaries=0
  declare -A roleOf
  for i in "${up[@]}"; do
    role=$(q "$(host "$i")" "SELECT role_desc FROM sys.dm_hadr_availability_replica_states WHERE is_local = 1")
    roleOf[$i]=$role
    if [[ "$role" = PRIMARY ]]; then primary=$i; primaries=$((primaries + 1)); fi
  done
  if [[ "$primaries" -gt 1 ]]; then
    echo "split brain: $primaries primaries; demote all but one with ALTER AVAILABILITY GROUP [$AG] SET (ROLE = SECONDARY)"
    for i in "${up[@]}"; do label "$STS-$i" conflict; done
    return
  fi
  for i in "${up[@]}"; do
    if [[ "${roleOf[$i]}" = PRIMARY ]]; then label "$STS-$i" primary; else label "$STS-$i" secondary; fi
  done
  [[ -z "$primary" ]] && { echo "no primary"; return; }
  local ph; ph=$(host "$primary")

  # A secondary's databases stop after a failover until they're resumed.
  for i in "${up[@]}"; do
    [[ "$i" = "$primary" ]] && continue
    for db in $(q "$(host "$i")" "SELECT DB_NAME(database_id) FROM sys.dm_hadr_database_replica_states WHERE is_local = 1 AND is_suspended = 1"); do
      echo "Resuming $db on $STS-$i"
      q "$(host "$i")" "ALTER DATABASE [$db] SET HADR RESUME" >/dev/null
    done
  done

  # New databases join the group.
  for db in $(q "$ph" "SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0 AND is_read_only = 0 AND name NOT IN (SELECT database_name FROM sys.availability_databases_cluster)"); do
    echo "Adding $db"
    q "$ph" "ALTER DATABASE [$db] SET RECOVERY FULL; BACKUP DATABASE [$db] TO DISK = N'NUL'; ALTER AVAILABILITY GROUP [$AG] ADD DATABASE [$db]" || true
  done

  # SQL logins (and their server roles) live in master, outside the group: copy them, with their SIDs, so a
  # database user still maps to its login after a failover.
  local rows name sid hash
  rows=$(q "$ph" "SELECT name + '|' + CONVERT(varchar(max), sid, 1) + '|' + CONVERT(varchar(max), password_hash, 1) FROM sys.sql_logins WHERE name NOT LIKE '##%' AND name <> 'sa' AND password_hash IS NOT NULL")
  for i in "${up[@]}"; do
    [[ "$i" = "$primary" ]] && continue
    h=$(host "$i")
    while IFS='|' read -r name sid hash; do
      [[ -z "$name" ]] && continue
      q "$h" "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$name') CREATE LOGIN [$name] WITH PASSWORD = $hash HASHED, SID = $sid, CHECK_POLICY = OFF; ELSE ALTER LOGIN [$name] WITH PASSWORD = $hash HASHED" >/dev/null
    done <<< "$rows"
    for r in $(q "$ph" "SELECT r.name + '|' + m.name FROM sys.server_role_members rm JOIN sys.server_principals r ON r.principal_id = rm.role_principal_id JOIN sys.server_principals m ON m.principal_id = rm.member_principal_id WHERE m.type = 'S' AND m.name NOT LIKE '##%' AND m.name <> 'sa'"); do
      q "$h" "ALTER SERVER ROLE [${r%%|*}] ADD MEMBER [${r##*|}]" >/dev/null
    done
  done
}

while true; do
  reconcile
  sleep "${INTERVAL:-20}"
done
