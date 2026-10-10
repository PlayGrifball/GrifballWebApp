#!/usr/bin/env bash
# End-to-end test of the chart on a real cluster: ./run.sh bundled|external|minimal
#
# Needs kubectl and helm pointed at an empty cluster that enforces NetworkPolicies (k3s/k3d do), and
# runs the app images BACKEND_IMAGE and FRONTEND_IMAGE (repository:tag; default the published :test
# ones - the workflow builds them from the checkout and imports them). The backend itself can't get ready without a real Discord bot, so
# this checks everything around it.
#
# bundled (the chart's SQL Server) and external (one in another namespace), test images:
#   1. install: the migration Job creates the database through the network policies; the backend
#      waits for it, then starts; the frontend gets ready;
#   2. sql-exporter reads SQL Server through its policy; a pod without a role reaches the frontend and
#      the exporter, but not the chart's SQL Server;
#   3. upgrade with nothing pending: a new Job checks, backs nothing up, migrates nothing, and
#      replaces the previous one;
#   4. the latest migration rolled back (the image's own bundle) and the backend restarted without
#      the Job: the backend waits, saying for what; an upgrade's Job backs up, applies it, and the
#      backend starts;
#   5. database dropped, backend restarted: it waits for the database; the Job recreates it with
#      nothing to back up.
# minimal: ci/minimal-values.yaml (the README's Quick start) as is, ingress class and images aside:
# the Job creates the database (not the app), the backend gets as far as Discord, the site is
# served through the Ingress (the cluster's Traefik, which k3s ships).
set -euo pipefail

scenario=${1:?usage: $0 bundled|external|minimal}
case "$scenario" in bundled|external|minimal) ;; *) echo "usage: $0 bundled|external|minimal" >&2; exit 2 ;; esac
chart=$(cd "$(dirname "$0")/.." && pwd)
ns=grif-e2e
ext_ns=external-sql
backend_image=${BACKEND_IMAGE:-ghcr.io/playgrifball/grifballwebappserver:test}
frontend_image=${FRONTEND_IMAGE:-ghcr.io/playgrifball/grifballwebappclient:test}
# Every helm install and upgrade runs these images, whatever the values say.
images=(--set "backend.image.repository=${backend_image%:*}" --set "backend.image.tag=${backend_image##*:}"
        --set "frontend.image.repository=${frontend_image%:*}" --set "frontend.image.tag=${frontend_image##*:}"
        --set backend.image.pullPolicy=IfNotPresent --set frontend.image.pullPolicy=IfNotPresent)
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
if [ "$scenario" != external ]; then sql_ns=$ns sql_deploy=grif-mssql pw_var=SA_PASSWORD
else sql_ns=$ext_ns sql_deploy=mssql pw_var=MSSQL_SA_PASSWORD; fi
sql() {
  kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- /bin/sh -c \
    '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(printenv "$0")" -C -b -h -1 -W -Q "SET NOCOUNT ON; $1"' "$pw_var" "$1"
}
migration_count() {
  sql "IF DB_ID(N'GrifballWebApp') IS NULL SELECT -1 ELSE SELECT COUNT(*) FROM GrifballWebApp.dbo.__EFMigrationsHistory" | tr -d '\r' | tail -1
}
# The newest migration Job.
latest_job() {
  kubectl -n "$ns" get jobs -l app.kubernetes.io/component=migrations \
    --sort-by=.metadata.creationTimestamp -o name | tail -1
}
# Wait for the newest migration Job (other than $1) to complete; prints its name.
wait_job() {
  local end=$(( $(date +%s) + 900 )) job=""
  while :; do
    job=$(latest_job)
    if [ -n "$job" ] && [ "$job" != "${1:-}" ]; then
      [ "$(kubectl -n "$ns" get "$job" -o jsonpath='{.status.succeeded}')" = 1 ] && { echo "$job"; return 0; }
      [ "$(kubectl -n "$ns" get "$job" -o jsonpath='{.status.failed}')" = "" ] || fail "$job failed"
    fi
    [ "$(date +%s)" -lt "$end" ] || fail "no migration Job finished"
    sleep 5
  done
}
job_logs() {
  local c
  for c in check-pending backup migrate; do
    echo "--- $c"; kubectl -n "$ns" logs "$1" -c "$c"
  done
}
backend_pod() { kubectl -n "$ns" get pods -l app=grif-backend -o jsonpath='{.items[0].metadata.name}'; }
# The backend pod's wait for the migrations has finished.
backend_waited() {
  [ "$(kubectl -n "$ns" get pods -l app=grif-backend \
    -o jsonpath='{.items[0].status.initContainerStatuses[?(@.name=="wait-for-migrations")].state.terminated.exitCode}')" = 0 ]
}
wait_logs() { kubectl -n "$ns" logs "$(backend_pod)" -c wait-for-migrations 2>/dev/null || true; }
# Restart the backend (Recreate: the old pod stops first).
restart_backend() {
  local old
  old=$(backend_pod)
  kubectl -n "$ns" rollout restart deploy/grif-backend >/dev/null
  wait_for 300 sh -c "kubectl -n $ns get pods -l app=grif-backend -o name | grep -v $old | grep -q ." || fail "no new backend pod"
}
# The backend pod is still waiting for migrations: in its init containers, saying what it waits for.
expect_waiting() {
  sleep 20
  backend_waited && fail "the backend started without the migration Job"
  wait_logs | grep -qF -- "$1" || { wait_logs >&2; fail "the backend isn't waiting for: $1"; }
  echo "The backend waits: $(wait_logs | tail -1)"
}
expect() { grep -qF -- "$1" "$2" || fail "expected \"$1\" in the Job's steps"; }
expect_not() { if grep -qF -- "$1" "$2"; then fail "didn't expect \"$1\" in the Job's steps"; fi; }

step "Namespaces and secrets ($scenario)"
kubectl create namespace "$ns"
# The token is shaped like a real one, so the backend gets as far as calling Discord.
kubectl -n "$ns" create secret generic grif-secrets \
  --from-literal=SA_PASSWORD="$password" \
  --from-literal=DiscordClientId=0 --from-literal=DiscordClientSecret=e2e \
  --from-literal=DiscordToken=MTIzNDU2Nzg5MDEyMzQ1Njc4.AAAAAA.e2e-not-a-real-token

if [ "$scenario" = minimal ]; then
  step "Install ci/minimal-values.yaml"
  helm install grif "$chart" -n "$ns" -f "$chart/ci/minimal-values.yaml" --set ingress.className=traefik "${images[@]}"
  job=$(wait_job)
  job_logs "$job" > "$work/install.txt"; cat "$work/install.txt"
  expect "doesn't exist: the migration creates it." "$work/install.txt"
  expected=$(grep -c "^Applying migration" "$work/install.txt" || true)
  count=$(migration_count)
  [ "$count" -gt 0 ] && [ "$count" = "$expected" ] || fail "database has $count migrations, the Job applied $expected"
  echo "The migration Job created the database: all $count."
  wait_for 300 backend_waited || fail "the backend didn't see the database up to date"
  backend_logs() {
    kubectl -n "$ns" logs deploy/grif-backend -c grif-backend --previous 2>/dev/null || true
    kubectl -n "$ns" logs deploy/grif-backend -c grif-backend 2>/dev/null || true
  }
  reached_discord() { backend_logs | grep -q '401 (Unauthorized)'; }
  wait_for 300 reached_discord || fail "the backend stopped before reaching Discord"
  if backend_logs | grep -q '"Applying migrations"'; then fail "the app migrated the database itself"; fi
  echo "The backend waited for the Job, got through its settings to Discord, which refuses the test"
  echo "token (401), and didn't migrate anything itself."

  step "The site through the ingress"
  wait_for 300 kubectl -n kube-system rollout status deploy/traefik --timeout=5s || fail "no Traefik"
  wait_for 300 kubectl -n "$ns" wait --for=condition=Ready pod -l app=grif-frontend --timeout=5s || fail "frontend not ready"
  kubectl -n "$ns" run curl --image=docker.io/curlimages/curl:8.16.0 --restart=Never --command -- sleep 600
  kubectl -n "$ns" wait --for=condition=Ready pod/curl --timeout=120s
  site() {
    kubectl -n "$ns" exec curl -- curl -fsk -H 'Host: grifball.example.com' https://traefik.kube-system.svc.cluster.local/ \
      | grep -q '<app-root'
  }
  wait_for 120 site || fail "the site isn't served through the ingress"
  echo "https://grifball.example.com/ serves the Angular app through the Ingress."

  step "PASS (minimal)"
  exit 0
fi

cat > "$work/values.yaml" <<EOF
backend:
  config:
    BaseUrl: https://grifball.example
    Discord:
      DraftChannel: "1"
  secretConfig:
    Discord:ClientId: DiscordClientId
    Discord:ClientSecret: DiscordClientSecret
    Discord:Token: DiscordToken
sqlExporter:
  enabled: true
EOF
if [ "$scenario" = bundled ]; then
  cat >> "$work/values.yaml" <<EOF
mssql:
  acceptEula: true
  resources: { requests: { memory: 1Gi, cpu: 100m }, limits: { memory: 2Gi } }
  persistence: { size: 2Gi }
  backup: { size: 1Gi }
migrations:
  backup: { retention: { enabled: true, keepLast: 1, keepDays: 0 } }
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
migrations:
  backup: { directory: /var/opt/mssql/backup/$ns, retention: { enabled: true, keepLast: 1, keepDays: 0 } }
EOF
fi

step "Install"
helm install grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job)
job_logs "$job" > "$work/install.txt"; cat "$work/install.txt"
expect "doesn't exist: the migration creates it." "$work/install.txt"
expect "No database to back up." "$work/install.txt"
expected=$(grep -c "^Applying migration" "$work/install.txt" || true)
count=$(migration_count)
[ "$count" -gt 0 ] && [ "$count" = "$expected" ] || fail "database has $count migrations, the Job applied $expected"
echo "The migration Job created the database through the network policies: all $count."
wait_for 300 backend_waited || fail "the backend didn't see the database up to date"
echo "The backend waited, then started: $(wait_logs | tail -1)"
wait_for 300 kubectl -n "$ns" wait --for=condition=Ready pod -l app=grif-frontend --timeout=5s || fail "frontend not ready"
echo "Frontend ready."

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

step "Upgrade, nothing pending"
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job "$job")
job_logs "$job" > "$work/upgrade.txt"; cat "$work/upgrade.txt"
expect "Up to date." "$work/upgrade.txt"
expect "Nothing to migrate: no backup needed." "$work/upgrade.txt"
expect "Nothing to migrate." "$work/upgrade.txt"
expect_not "BACKUP DATABASE" "$work/upgrade.txt"
[ "$(migration_count)" = "$count" ] || fail "migrations changed"
wait_for 60 sh -c "[ \$(kubectl -n $ns get jobs -l app.kubernetes.io/component=migrations -o name | wc -l) = 1 ]" \
  || fail "the previous migration Job is still there"
echo "A new Job checked, found nothing to do, and replaced the previous one."

step "A pending migration: the backend waits; the Job backs up, then applies it"
latest=$(sql "SELECT TOP 1 MigrationId FROM GrifballWebApp.dbo.__EFMigrationsHistory ORDER BY MigrationId DESC" | tr -d '\r' | tail -1)
previous=$(sql "SELECT MigrationId FROM GrifballWebApp.dbo.__EFMigrationsHistory ORDER BY MigrationId DESC OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY" | tr -d '\r' | tail -1)
image=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].image}')
conn=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="ConnectionStrings__GrifballWebApp")].value}')
db_secret=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="DB_PASSWORD")].valueFrom.secretKeyRef.name}')
db_key=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="DB_PASSWORD")].valueFrom.secretKeyRef.key}')
echo "Rolling $latest back to $previous with the image's bundle"
# Labelled as the migration Job so the network policies let it reach the database.
kubectl -n "$ns" run rollback --image="$image" --restart=Never --labels=app=grif-migrate --overrides="$(cat <<EOF
{"spec": {"containers": [{"name": "rollback", "image": "$image", "command": ["/app/efbundle", "$previous"],
  "env": [{"name": "DB_PASSWORD", "valueFrom": {"secretKeyRef": {"name": "$db_secret", "key": "$db_key"}}},
          {"name": "ConnectionStrings__GrifballWebApp", "value": "$conn"}]}]}}
EOF
)"
wait_for 300 sh -c "kubectl -n $ns get pod rollback -o jsonpath='{.status.phase}' | grep -q Succeeded" \
  || { kubectl -n "$ns" logs rollback >&2 || true; fail "rollback failed"; }
kubectl -n "$ns" delete pod rollback --wait=true >/dev/null
[ "$(migration_count)" = $(( count - 1 )) ] || fail "rollback didn't remove $latest"
restart_backend
expect_waiting "Waiting for the migration Job to apply: $latest"
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job "$job")
job_logs "$job" > "$work/pending.txt"; cat "$work/pending.txt"
expect "Pending migrations:" "$work/pending.txt"
expect "$latest" "$work/pending.txt"
expect "BACKUP DATABASE successfully processed" "$work/pending.txt"
expect "Applying migration '$latest'" "$work/pending.txt"
[ "$(migration_count)" = "$count" ] || fail "$latest not applied again"
kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "ls /var/opt/mssql/backup/$ns/GrifballWebApp_*.bak" || fail "backup file missing"
wait_for 120 backend_waited || fail "the backend didn't start after the Job"
echo "The Job backed up and applied $latest; the backend then started."

step "Database dropped: the backend waits; the Job recreates it, nothing to back up"
sql "ALTER DATABASE GrifballWebApp SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE GrifballWebApp;"
[ "$(migration_count)" = -1 ] || fail "database not dropped"
restart_backend
expect_waiting "Waiting for the migration Job to create database GrifballWebApp."
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job "$job")
job_logs "$job" > "$work/dropped.txt"; cat "$work/dropped.txt"
expect "doesn't exist: the migration creates it." "$work/dropped.txt"
expect "No database to back up." "$work/dropped.txt"
[ "$(migration_count)" = "$count" ] || fail "the Job didn't recreate the database"
wait_for 120 backend_waited || fail "the backend didn't start after the Job"
echo "Database recreated with all $count migrations; the backend then started."

step "Backup retention: the newest backup kept, older ones deleted, anything else left alone"
dir=/var/opt/mssql/backup/$ns
for n in 1 2; do
  sql "BACKUP DATABASE GrifballWebApp TO DISK = N'$dir/GrifballWebApp_2026010${n}_000000.bak' WITH FORMAT, INIT;" >/dev/null
  sleep 2
done
# Named like a backup, but not one SQL Server wrote.
kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "echo junk > $dir/GrifballWebApp_20200101_000000.bak"
latest_bak=$(kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "ls -t $dir/GrifballWebApp_2026*.bak | head -1")
before=$(kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "ls $dir/GrifballWebApp_*.bak | wc -l")
kubectl -n "$ns" create job retention-now --from=cronjob/grif-backup-retention
wait_for 300 sh -c "kubectl -n $ns get job retention-now -o jsonpath='{.status.succeeded}' | grep -q 1" \
  || { kubectl -n "$ns" logs job/retention-now >&2 || true; fail "the retention job didn't succeed"; }
kubectl -n "$ns" logs job/retention-now | tee "$work/retention.txt"
left=$(kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "ls $dir/GrifballWebApp_*.bak")
echo "$left"
echo "$left" | grep -qxF "$latest_bak" || fail "the newest backup was deleted"
echo "$left" | grep -qxF "$dir/GrifballWebApp_20200101_000000.bak" || fail "a file SQL Server didn't write was deleted"
[ "$(echo "$left" | wc -l)" = 2 ] || fail "expected the newest backup and the stray file left, of $before"
expect "Deleted $(( before - 2 )) backup(s)" "$work/retention.txt"
echo "Kept the newest backup and the stray file; deleted the other $(( before - 2 ))."

step "PASS ($scenario)"
