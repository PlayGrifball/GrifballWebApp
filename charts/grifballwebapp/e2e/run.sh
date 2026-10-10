#!/usr/bin/env bash
# End-to-end test of the chart on a real cluster: ./run.sh bundled|external
#
# Needs kubectl and helm pointed at an empty cluster that enforces NetworkPolicies (k3s/k3d do), and
# pulls the published app images (BACKEND_TAG / FRONTEND_TAG, default test). The backend itself can't
# get ready without a real Discord bot, so this checks everything around it:
#   1. install: SQL Server (the chart's, or an "external" one in another namespace) comes up, the
#      backend's databaseSetup init containers create the database through the network policies,
#      the frontend gets ready;
#   2. sql-exporter reads SQL Server through its policy (its metrics carry the queries' results); a
#      pod without a role reaches the frontend and the exporter but not SQL Server;
#   3. upgrade: the deploy hook finds nothing pending, keeps the app up and writes a backup;
#   4. upgrade after the database is dropped: the hook finds everything pending, scales the app
#      down, skips the backup and the migration recreates the database.
set -euo pipefail

scenario=${1:?usage: $0 bundled|external}
case "$scenario" in bundled|external) ;; *) echo "usage: $0 bundled|external" >&2; exit 2 ;; esac
chart=$(cd "$(dirname "$0")/.." && pwd)
ns=grif-e2e
ext_ns=external-sql
backend_tag=${BACKEND_TAG:-test}
frontend_tag=${FRONTEND_TAG:-test}
sql_image=mcr.microsoft.com/mssql/server:2025-latest
password="E2e!$(head -c 12 /dev/urandom | od -An -tx1 | tr -d ' \n')Aa1"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

step() { echo; echo "=== $*"; }
fail() { echo "FAIL: $*" >&2; diagnose; exit 1; }
diagnose() {
  echo "--- pods" >&2
  kubectl get pods -A -o wide >&2 || true
  for pod in $(kubectl -n "$ns" get pods -o name 2>/dev/null); do
    echo "--- $pod" >&2
    kubectl -n "$ns" describe "$pod" | sed -n '/^Events:/,$p' >&2 || true
    for c in $(kubectl -n "$ns" get "$pod" -o jsonpath='{.spec.initContainers[*].name} {.spec.containers[*].name}'); do
      echo "--- $pod $c" >&2
      kubectl -n "$ns" logs "$pod" -c "$c" --tail=40 >&2 || true
    done
  done
}
# Wait up to $1 seconds for "$2..." to succeed.
wait_for() {
  local timeout=$1; shift
  local end=$(( $(date +%s) + timeout ))
  until "$@" >/dev/null 2>&1; do
    [ "$(date +%s)" -lt "$end" ] || return 1
    sleep 5
  done
}

# sqlcmd inside the SQL Server container, so the test itself doesn't need a network path. The chart
# gives SQL Server SA_PASSWORD; the external one here uses MSSQL_SA_PASSWORD.
if [ "$scenario" = bundled ]; then sql_ns=$ns sql_deploy=grif-mssql pw_var=SA_PASSWORD
else sql_ns=$ext_ns sql_deploy=mssql pw_var=MSSQL_SA_PASSWORD; fi
sql() {
  kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- /bin/sh -c \
    '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(printenv "$0")" -C -b -h -1 -W -Q "SET NOCOUNT ON; $1"' "$pw_var" "$1"
}
migration_count() {
  sql "IF DB_ID(N'GrifballWebApp') IS NULL SELECT -1 ELSE SELECT COUNT(*) FROM GrifballWebApp.dbo.__EFMigrationsHistory" | tr -d '\r' | tail -1
}
hook_logs() {
  local pod
  pod=$(kubectl -n "$ns" get pods -l app=grif-deploy-hook -o name | head -1)
  for c in wait-for-database check-pending scale-down backup migrate; do
    echo "--- $c"; kubectl -n "$ns" logs "$pod" -c "$c"
  done
}

step "Namespaces and secrets ($scenario)"
kubectl create namespace "$ns"
kubectl -n "$ns" create secret generic grif-secrets \
  --from-literal=SA_PASSWORD="$password" \
  --from-literal=DiscordClientId=0 --from-literal=DiscordClientSecret=e2e \
  --from-literal=DiscordToken=e2e.not.a.token --from-literal=DiscordDraftChannel=1

cat > "$work/values.yaml" <<EOF
backend:
  image: { tag: "$backend_tag", pullPolicy: IfNotPresent }
  config:
    ApplyMigrations: false
    CreateDatabase: false
    BaseUrl: https://grifball.example
  secretConfig:
    Discord:ClientId: DiscordClientId
    Discord:ClientSecret: DiscordClientSecret
    Discord:Token: DiscordToken
    Discord:DraftChannel: DiscordDraftChannel
frontend:
  image: { tag: "$frontend_tag", pullPolicy: IfNotPresent }
databaseSetup:
  enabled: true
sqlExporter:
  enabled: true
deployHook:
  databaseWaitSeconds: 300
EOF

if [ "$scenario" = bundled ]; then
  cat >> "$work/values.yaml" <<EOF
mssql:
  acceptEula: true
  image: { tag: 2025-latest }
  resources: { requests: { memory: 1Gi, cpu: 100m }, limits: { memory: 2Gi } }
  persistence: { size: 2Gi }
  # local-path (k3s) provisions ReadWriteOnce only.
  backup: { accessModes: [ReadWriteOnce], size: 1Gi }
EOF
else
  step "External SQL Server in $ext_ns"
  kubectl create namespace "$ext_ns"
  kubectl -n "$ext_ns" create secret generic mssql --from-literal=password="$password"
  kubectl -n "$ns" create secret generic external-db --from-literal=password="$password"
  kubectl -n "$ext_ns" apply -f - <<EOF
apiVersion: apps/v1
kind: Deployment
metadata: { name: mssql }
spec:
  selector: { matchLabels: { app: mssql } }
  template:
    metadata: { labels: { app: mssql } }
    spec:
      containers:
        - name: mssql
          image: $sql_image
          env:
            - { name: ACCEPT_EULA, value: "Y" }
            - name: MSSQL_SA_PASSWORD
              valueFrom: { secretKeyRef: { name: mssql, key: password } }
          ports: [{ containerPort: 1433 }]
          resources: { requests: { memory: 1Gi }, limits: { memory: 2Gi } }
---
apiVersion: v1
kind: Service
metadata: { name: mssql }
spec:
  selector: { app: mssql }
  ports: [{ port: 1433, targetPort: 1433 }]
EOF
  cat >> "$work/values.yaml" <<EOF
mssql:
  enabled: false
database:
  host: mssql.$ext_ns.svc.cluster.local
  port: 1433
  user: sa
  password: { secretName: external-db, key: password }
deployHook:
  databaseWaitSeconds: 300
  backup: { directory: /var/opt/mssql/backup/$ns }
EOF
fi

step "Install"
helm install grif "$chart" -n "$ns" -f "$work/values.yaml"
setup_done() {
  [ "$(kubectl -n "$ns" get pods -l app=grif-backend \
    -o jsonpath='{.items[0].status.initContainerStatuses[?(@.name=="database-setup")].state.terminated.exitCode}')" = 0 ]
}
wait_for 900 setup_done || fail "the backend's database-setup didn't finish"
echo "databaseSetup created the database through the network policies."
wait_for 300 kubectl -n "$ns" wait --for=condition=Ready pod -l app=grif-frontend --timeout=5s || fail "frontend not ready"
echo "Frontend ready."
expected=$(kubectl -n "$ns" logs "$(kubectl -n "$ns" get pods -l app=grif-backend -o name | head -1)" -c database-setup | grep -c "^Applying migration" || true)
count=$(migration_count)
[ "$count" -gt 0 ] && [ "$count" = "$expected" ] || fail "database has $count migrations, setup applied $expected"
echo "Database has all $count migrations."

step "Network policies and sql-exporter"
kubectl -n "$ns" run intruder --image=docker.io/library/busybox:1.38 --restart=Never --command -- sleep 600
kubectl -n "$ns" wait --for=condition=Ready pod/intruder --timeout=120s
kubectl -n "$ns" exec intruder -- nc -w 3 grif-frontend 80 </dev/null || fail "intruder can't reach the frontend (control)"
exporter_reads_sql() {
  kubectl -n "$ns" exec intruder -- wget -qO- http://sql-exporter:9399/metrics | grep -q '^mssql_connections{'
}
wait_for 300 exporter_reads_sql || fail "sql-exporter has no SQL Server metrics"
echo "sql-exporter reads SQL Server; its metrics are reachable."
if [ "$scenario" = bundled ]; then
  if kubectl -n "$ns" exec intruder -- nc -w 3 sqlserver 1433 </dev/null; then fail "intruder reached SQL Server"; fi
  echo "A pod without a role reaches the frontend but not SQL Server."
fi
kubectl -n "$ns" delete pod intruder --wait=false

step "Upgrade: nothing pending"
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" --timeout 15m || { hook_logs; fail "upgrade failed"; }
hook_logs > "$work/hook1.txt"; cat "$work/hook1.txt"
grep -q "No pending migrations: the app stays up." "$work/hook1.txt" || fail "hook found migrations pending"
grep -q "Nothing pending: not scaling down." "$work/hook1.txt" || fail "hook scaled down"
grep -q "BACKUP DATABASE successfully processed" "$work/hook1.txt" || fail "no backup"
kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "ls /var/opt/mssql/backup/$ns/GrifballWebApp_*.bak" || fail "backup file missing"

step "Upgrade after the database is dropped"
sql "ALTER DATABASE GrifballWebApp SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE GrifballWebApp;"
[ "$(migration_count)" = -1 ] || fail "database not dropped"
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" --timeout 15m || { hook_logs; fail "upgrade failed"; }
hook_logs > "$work/hook2.txt"; cat "$work/hook2.txt"
grep -q "Pending migrations:" "$work/hook2.txt" || fail "hook found nothing pending"
grep -q "Scaled down" "$work/hook2.txt" || fail "hook didn't scale down"
grep -q "No database to back up." "$work/hook2.txt" || fail "hook tried to back up a missing database"
[ "$(migration_count)" = "$count" ] || fail "migration didn't recreate the database"
[ "$(kubectl -n "$ns" get deploy grif-frontend -o jsonpath='{.spec.replicas}')" = 1 ] || fail "frontend not scaled back up"
echo "Database recreated with all $count migrations; app scaled back up."

step "PASS ($scenario)"
