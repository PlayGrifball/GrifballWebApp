#!/usr/bin/env bash
# End-to-end test of the chart on a real cluster: ./run.sh bundled|external|postgres|minimal|scaleout
#
# Needs kubectl and helm pointed at an empty cluster that enforces NetworkPolicies (k3s/k3d do), and
# runs the app images BACKEND_IMAGE and FRONTEND_IMAGE (repository:tag; default the published :test
# ones - the workflow builds them from the checkout and imports them). The backend itself can't get ready without a real Discord bot, so
# this checks everything around it.
#
# bundled (the chart's SQL Server), external (one in another namespace) and postgres (the chart's
# PostgreSQL, database.provider postgres), test images:
#   1. install: the migration Job creates the database through the network policies; the backend
#      waits for it, then starts; the frontend gets ready;
#   2. sql-exporter reads the database through its policy; a pod without a role reaches the frontend
#      and the exporter, but not the chart's database server;
#      the least-privilege logins (database.logins): the backend connects as grif_app, which can't
#      change the schema or the server; grif_monitor (sql-exporter) can't read the app's data;
#   3. upgrade with nothing pending: a new Job checks, backs nothing up, migrates nothing, and
#      replaces the previous one;
#   4. the latest migration rolled back (the image's own bundle) and the backend restarted without
#      the Job: the backend waits, saying for what; an upgrade's Job backs up, applies it, and the
#      backend starts;
#   5. database dropped, backend restarted: it waits for the database; the Job recreates it with
#      nothing to back up;
#   5a. preview reset: a migration the image lacks, none of master's, recorded with a marker table: an
#      upgrade without backend.config.Preview.Enabled leaves both; one with it drops the database and
#      migrates it again (the image's migrations, no marker), and a second finds nothing to reset;
#   6. the scheduled backup CronJob run by hand, then backup retention: the newest kept, older ones
#      deleted, anything else in the folder left alone. Both CronJobs are installed suspended, so their
#      own schedule (03:00 and 03:30 UTC) can't run beside the test's (two failures, 2026-10-12);
#   7. restore (migrations.restore): a marked database backed up, then a first deploy restores it -
#      SQL Server: the same release with its database dropped (external: and made again, empty, as a
#      managed server's often is), from its own backup folder;
#      PostgreSQL: a second release in another namespace, from a volume holding the first's dump;
#      then again into an empty database made beforehand;
#   8. convert (migrations.convert): a release in another namespace copies the first release's
#      database on its first deploy - bundled into PostgreSQL, postgres into SQL Server, external into
#      another database on the external server made beforehand with no tables; a changed row and its
#      history come across, and its backend starts.
# minimal: ci/minimal-values.yaml (the README's Quick start) as is, ingress class and images aside:
# the Job creates the database (not the app), the backend gets as far as Discord, the site is
# served through the Ingress (the cluster's Traefik, which k3s ships).
# scaleout: worker.enabled and redis.enabled with two backend replicas and no state volume (the chart's
# PostgreSQL): the two API pods get ready (no Discord gateway to fail on), the worker gets as far as
# Discord, both API pods' SignalR hubs are on the backplane, nothing else reaches Redis or the worker,
# and the backend rolls (RollingUpdate) while the worker is recreated. It also runs the bundled Halo
# gateway (halo-gateway.enabled): ready, called by the backend and the worker with the key from its
# Secret, reachable from their pods only.
set -euo pipefail

scenario=${1:?usage: $0 bundled|external|postgres|minimal|scaleout}
case "$scenario" in bundled|external|postgres|minimal|scaleout) ;; *) echo "usage: $0 bundled|external|postgres|minimal|scaleout" >&2; exit 2 ;; esac
chart=$(cd "$(dirname "$0")/.." && pwd)
ns=grif-e2e
ext_ns=external-sql
restore_ns=grif-e2e-restore
backend_image=${BACKEND_IMAGE:-ghcr.io/playgrifball/grifballwebappserver:test}
frontend_image=${FRONTEND_IMAGE:-ghcr.io/playgrifball/grifballwebappclient:test}
# Every helm install and upgrade runs these images, whatever the values say.
images=(--set "backend.image.repository=${backend_image%:*}" --set "backend.image.tag=${backend_image##*:}"
        --set "frontend.image.repository=${frontend_image%:*}" --set "frontend.image.tag=${frontend_image##*:}"
        --set backend.image.pullPolicy=IfNotPresent --set frontend.image.pullPolicy=IfNotPresent)
sql_image=mcr.microsoft.com/mssql/server:2025-latest
password="E2e!$(head -c 12 /dev/urandom | od -An -tx1 | tr -d ' \n')Aa1"
# The least-privilege logins' passwords, with a quote and a $( ) to prove they're escaped.
app_password='App'"'"'$(x)!'"$(head -c 8 /dev/urandom | od -An -tx1 | tr -d ' \n')Aa1"
mon_password='Mon'"'"'$(y)!'"$(head -c 8 /dev/urandom | od -An -tx1 | tr -d ' \n')Bb2"
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
# psql inside the chart's PostgreSQL container (namespace $ns), as postgres over its socket, which the
# image trusts: $1 the database, $2 the SQL. Unaligned, tuples only.
pg() {
  kubectl -n "$ns" exec deploy/grif-postgres -c grif-postgres -- psql -X -A -t -q -v ON_ERROR_STOP=1 -U postgres -d "$1" -c "$2"
}
migration_count() {
  if [ "$scenario" = postgres ] || [ "$scenario" = scaleout ]; then
    if [ "$(pg postgres "SELECT count(*) FROM pg_database WHERE datname = 'GrifballWebApp'")" != 1 ]; then echo -1; return; fi
    pg GrifballWebApp 'SELECT count(*) FROM "__EFMigrationsHistory"'
  else
    sql "IF DB_ID(N'GrifballWebApp') IS NULL SELECT -1 ELSE SELECT COUNT(*) FROM GrifballWebApp.dbo.__EFMigrationsHistory" | tr -d '\r' | tail -1
  fi
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
# Every step's log, in order (the restore step only when it's on).
job_logs() {
  local c out
  for c in $(kubectl -n "$ns" get "$1" -o jsonpath='{.spec.template.spec.initContainers[*].name} {.spec.template.spec.containers[*].name}'); do
    out=$(kubectl -n "$ns" logs "$1" -c "$c")
    echo "--- $c"; echo "$out"
    # A shell error in a step's script (a command it couldn't find) fails the run even if the step passed.
    if echo "$out" | grep -q ": not found"; then fail "a shell error in the Job's $c step: $(echo "$out" | grep ": not found" | head -1)"; fi
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
# PostgreSQL's backups are on a volume (migrations.backup.volume): runs $1 in a pod of the backend
# image (it has pg_restore) that mounts it, started the first time.
backups() {
  if ! kubectl -n "$ns" get pod backups >/dev/null 2>&1; then
    kubectl -n "$ns" run backups --image="$backend_image" --restart=Never --overrides="$(cat <<EOF
{"spec": {"securityContext": {"runAsUser": 1654, "runAsGroup": 1654, "fsGroup": 1654},
  "containers": [{"name": "backups", "image": "$backend_image", "imagePullPolicy": "IfNotPresent",
    "command": ["sleep", "3600"], "volumeMounts": [{"name": "backup", "mountPath": "/backup"}]}],
  "volumes": [{"name": "backup", "persistentVolumeClaim": {"claimName": "postgres-backup-pvc"}}]}}
EOF
)" >/dev/null
    kubectl -n "$ns" wait --for=condition=Ready pod/backups --timeout=120s >/dev/null
  fi
  kubectl -n "$ns" exec backups -- sh -c "$1"
}

step "Namespaces and secrets ($scenario)"
kubectl create namespace "$ns"
# The token is shaped like a real one, so the backend gets as far as calling Discord.
kubectl -n "$ns" create secret generic grif-secrets \
  --from-literal=SA_PASSWORD="$password" --from-literal=POSTGRES_PASSWORD="$password" \
  --from-literal=DiscordClientId=0 --from-literal=DiscordClientSecret=e2e \
  --from-literal=APP_DB_PASSWORD="$app_password" --from-literal=MONITOR_DB_PASSWORD="$mon_password" \
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
      | grep '<app-root' >/dev/null  # not -q: curl fails (pipefail) when grep stops reading early
  }
  wait_for 120 site || fail "the site isn't served through the ingress"
  echo "https://grifball.example.com/ serves the Angular app through the Ingress."

  step "PASS (minimal)"
  exit 0
fi

if [ "$scenario" = scaleout ]; then
  step "Install: two API pods, the worker, the chart's Redis and its own Halo gateway, no state volume"
  # The key the bundled gateway accepts and the backend sends: one Secret both read.
  kubectl -n "$ns" create secret generic halo-gateway-key --from-literal=ApiKey=e2e-gateway-key
  cat > "$work/values.yaml" <<EOF
backend:
  replicas: 2
  persistence: { enabled: false }
  config:
    BaseUrl: https://grifball.example
    Discord:
      DraftChannel: "1"
  secretConfig:
    Discord:ClientId: DiscordClientId
    Discord:ClientSecret: DiscordClientSecret
    Discord:Token: DiscordToken
database:
  provider: postgres
postgres:
  persistence: { size: 1Gi }
worker:
  enabled: true
redis:
  enabled: true
# The halo-gateway subchart: no Microsoft app or Halo account, so it answers but has no token.
halo-gateway:
  enabled: true
  oauth: { redirectUrl: https://halo-gateway.example/oauth/callback }
  apiKeys:
    - { name: grif-e2e, environment: e2e, priority: 0, permitsPerMinute: 600, secretName: halo-gateway-key, secretKey: ApiKey }
  persistence: { enabled: false }
EOF
  helm install grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
  job=$(wait_job)
  job_logs "$job" > "$work/install.txt"; cat "$work/install.txt"
  count=$(migration_count)
  [ "$count" -gt 0 ] || fail "the migration Job didn't create the database"
  echo "The migration Job created the database: $count migrations."

  deploy_field() { kubectl -n "$ns" get deploy "$1" -o jsonpath="$2"; }
  [ "$(deploy_field grif-backend '{.spec.strategy.type}')" = RollingUpdate ] || fail "the backend doesn't roll"
  [ "$(deploy_field grif-worker '{.spec.strategy.type}')" = Recreate ] || fail "the worker isn't recreated"
  [ "$(deploy_field grif-worker '{.spec.replicas}')" = 1 ] || fail "not exactly one worker"
  role_of() { deploy_field "$1" '{.spec.template.spec.containers[0].env[?(@.name=="App__Role")].value}'; }
  [ "$(role_of grif-backend)" = api ] && [ "$(role_of grif-worker)" = worker ] || fail "roles: backend $(role_of grif-backend), worker $(role_of grif-worker)"
  echo "grif-backend: App:Role api, RollingUpdate; grif-worker: App:Role worker, one pod, Recreate."

  # The API role has no Discord gateway, so with the test's token the API pods still get ready.
  api_ready() { [ "$(deploy_field grif-backend '{.status.readyReplicas}')" = 2 ]; }
  wait_for 600 api_ready || fail "the two API pods aren't ready"
  echo "Both API pods are ready."
  worker_logs() {
    kubectl -n "$ns" logs deploy/grif-worker -c grif-backend --previous 2>/dev/null || true
    kubectl -n "$ns" logs deploy/grif-worker -c grif-backend 2>/dev/null || true
  }
  worker_reached_discord() { worker_logs | grep -q '401 (Unauthorized)'; }
  wait_for 300 worker_reached_discord || fail "the worker didn't get as far as Discord"
  for pod in $(kubectl -n "$ns" get pods -l app=grif-backend -o name); do
    if kubectl -n "$ns" logs "$pod" -c grif-backend | grep -q '401 (Unauthorized)'; then fail "$pod (API) called Discord's gateway"; fi
  done
  echo "The worker runs the Discord gateway (Discord refuses the test token); the API pods don't."

  # Each API pod's TeamsHub subscribes to the backplane when it starts.
  hub_subscribers() {
    kubectl -n "$ns" exec deploy/grif-redis -- valkey-cli PUBSUB NUMSUB "GrifballWebAppGrifballWebApp.Server.Teams.TeamsHub:all" | tail -1
  }
  two_subscribers() { [ "$(hub_subscribers)" = 2 ]; }
  wait_for 120 two_subscribers || fail "the backplane has $(hub_subscribers) hub subscribers, not 2"
  echo "Both API pods' SignalR hubs are on the backplane."

  step "Network policies"
  kubectl -n "$ns" run intruder --image=docker.io/library/busybox:1.38 --restart=Never --command -- sleep 600
  kubectl -n "$ns" wait --for=condition=Ready pod/intruder --timeout=120s
  kubectl -n "$ns" exec intruder -- nc -w 3 grif-frontend 80 </dev/null || fail "intruder can't reach the frontend (control)"
  if kubectl -n "$ns" exec intruder -- nc -w 3 grif-redis 6379 </dev/null; then fail "intruder reached Redis"; fi
  worker_ip=$(kubectl -n "$ns" get pods -l app=grif-worker -o jsonpath='{.items[0].status.podIP}')
  if kubectl -n "$ns" exec intruder -- nc -w 3 "$worker_ip" 5295 </dev/null; then fail "intruder reached the worker"; fi
  echo "A pod without a role reaches the frontend, but not Redis or the worker."

  step "The bundled Halo gateway"
  kubectl -n "$ns" rollout status deploy/grif-halo-gateway --timeout=300s || fail "the bundled gateway didn't get ready"
  env_of() { deploy_field "$1" "{.spec.template.spec.containers[0].env[?(@.name==\"$2\")]$3}"; }
  for d in grif-backend grif-worker; do
    [ "$(env_of $d HaloGateway__BaseUrl .value)" = http://grif-halo-gateway:8080/ ] || fail "$d calls $(env_of $d HaloGateway__BaseUrl .value)"
    [ "$(env_of $d HaloGateway__ApiKey .valueFrom.secretKeyRef.name)" = halo-gateway-key ] || fail "$d doesn't read the gateway's key Secret"
  done
  echo "The gateway is ready; the backend and the worker call its Service with the key from its Secret."
  # A pod with the worker's label gets the worker's egress, and the gateway admits it (grif-halo-gateway-ingress).
  kubectl -n "$ns" run as-worker --image=docker.io/curlimages/curl:8.16.0 --labels=app=grif-worker --restart=Never --command -- sleep 600
  kubectl -n "$ns" wait --for=condition=Ready pod/as-worker --timeout=120s
  gw=http://grif-halo-gateway:8080
  gw_status() { kubectl -n "$ns" exec as-worker -- curl -sS --max-time 10 -o /dev/null -w '%{http_code}' "$@"; }
  [ "$(gw_status "$gw/health/live")" = 200 ] || fail "the worker's pods can't reach the gateway"
  [ "$(gw_status "$gw/halo/v1/medals")" = 401 ] || fail "the gateway took a call without a key"
  status=$(gw_status -H 'X-Api-Key: e2e-gateway-key' "$gw/halo/v1/medals" || true)
  [ "$status" != 401 ] && [ "$status" != 000 ] || fail "the gateway refused grif's key ($status)"
  if kubectl -n "$ns" exec intruder -- nc -w 3 grif-halo-gateway 8080 </dev/null; then fail "intruder reached the gateway"; fi
  kubectl -n "$ns" delete pod as-worker --wait=false >/dev/null
  echo "The worker's pods reach the gateway, which takes grif's key ($status: no Halo account signed in) and"
  echo "refuses calls without it; a pod without a role can't reach it."

  step "The backend rolls"
  kubectl -n "$ns" rollout restart deploy/grif-backend >/dev/null
  kubectl -n "$ns" rollout status deploy/grif-backend --timeout=600s || fail "the backend's rolling update didn't finish"
  wait_for 120 two_subscribers || fail "after the roll, the backplane has $(hub_subscribers) hub subscribers, not 2"
  echo "The backend rolled to two new pods, both back on the backplane."

  step "PASS (scaleout)"
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
database:
  logins: { enabled: true }
mssql:
  acceptEula: true
  resources: { requests: { memory: 1Gi, cpu: 100m }, limits: { memory: 2Gi } }
  persistence: { size: 2Gi }
  backup: { size: 1Gi }
migrations:
  backup: { retention: { enabled: true, suspend: true, keepLast: 1, keepDays: 0 }, scheduled: { enabled: true, suspend: true } }
EOF
elif [ "$scenario" = postgres ]; then
  cat >> "$work/values.yaml" <<EOF
database:
  provider: postgres
  logins: { enabled: true }
postgres:
  persistence: { size: 1Gi }
  # Each connection logged as it's authorized, with its TLS (the TLS step reads them).
  settings: { log_connections: authorization }
migrations:
  backup: { volume: { size: 1Gi }, retention: { enabled: true, suspend: true, keepLast: 1, keepDays: 0 }, scheduled: { enabled: true, suspend: true } }
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
  logins: { enabled: true }
migrations:
  backup: { directory: /var/opt/mssql/backup/$ns, retention: { enabled: true, suspend: true, keepLast: 1, keepDays: 0 }, scheduled: { enabled: true, suspend: true } }
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
if [ "$scenario" = postgres ]; then metric=pg_connections; else metric=mssql_connections; fi
exporter_reads_sql() {
  kubectl -n "$ns" exec intruder -- wget -qO- http://sql-exporter:9399/metrics | grep -q "^$metric{"
}
wait_for 300 exporter_reads_sql || fail "sql-exporter has no $metric metrics"
echo "sql-exporter reads the database; its metrics are reachable."
if [ "$scenario" = bundled ]; then
  if kubectl -n "$ns" exec intruder -- nc -w 3 sqlserver 1433 </dev/null; then fail "intruder reached SQL Server"; fi
  echo "A pod without a role reaches the frontend but not SQL Server."
elif [ "$scenario" = postgres ]; then
  if kubectl -n "$ns" exec intruder -- nc -w 3 postgres 5432 </dev/null; then fail "intruder reached PostgreSQL"; fi
  echo "A pod without a role reaches the frontend but not PostgreSQL."
fi
kubectl -n "$ns" delete pod intruder --wait=false

step "Least-privilege logins"
if [ "$scenario" = postgres ]; then
  # As each role, over TCP with its password, inside PostgreSQL's container.
  as_login() {
    local user=$1 pw=$2 db=$3 query=$4
    kubectl -n "$ns" exec deploy/grif-postgres -c grif-postgres -- env PGPASSWORD="$pw" \
      psql -X -A -t -q -v ON_ERROR_STOP=1 -h 127.0.0.1 -U "$user" -d "$db" -c "$query"
  }
  [ "$(kubectl -n "$ns" get deploy grif-backend -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="ConnectionStrings__GrifballWebApp")].value}' | grep -o 'Username=[^;]*')" = "Username=grif_app" ] \
    || fail "the backend doesn't connect as grif_app"
  as_login grif_app "$app_password" GrifballWebApp 'SELECT count(*) FROM "__EFMigrationsHistory"' | grep -qx "$count" \
    || fail "grif_app can't read the database"
  if as_login grif_app "$app_password" GrifballWebApp 'CREATE TABLE public.nope (id int)' 2>/dev/null; then fail "grif_app changed the schema"; fi
  if as_login grif_app "$app_password" GrifballWebApp 'CREATE SCHEMA nope' 2>/dev/null; then fail "grif_app created a schema"; fi
  if as_login grif_app "$app_password" postgres 'CREATE ROLE nope' 2>/dev/null; then fail "grif_app created a role"; fi
  as_login grif_monitor "$mon_password" postgres 'SELECT count(*) FROM pg_stat_activity' >/dev/null \
    || fail "grif_monitor can't read server statistics"
  if as_login grif_monitor "$mon_password" GrifballWebApp 'SELECT count(*) FROM "__EFMigrationsHistory"' 2>/dev/null; then fail "grif_monitor read the app's tables"; fi
  echo "The backend connects as grif_app, which reads and writes but can't change the schema or the server;"
  echo "grif_monitor reads server statistics (sql-exporter's metrics) and can't read the app's tables."

  step "TLS: PostgreSQL serves it with its self-signed certificate; the backend's connections use it"
  [ "$(pg postgres 'SHOW ssl')" = on ] || fail "PostgreSQL isn't serving TLS"
  # From PostgreSQL's log (log_connections), not its live connections: the backend here can't stay up
  # (Discord refuses the test credentials), so it holds a connection only for a moment each start.
  # Every grif_app connection - the app's own (Npgsql, no application_name) and psql's - over TLS.
  grif_app_connections() {
    kubectl -n "$ns" logs deploy/grif-postgres -c grif-postgres | grep 'connection authorized: user=grif_app ' || true
  }
  app_connected() { grif_app_connections | grep -qv 'application_name=psql'; }
  wait_for 120 app_connected || fail "the backend never connected as grif_app: $(grif_app_connections | tail -3)"
  if grif_app_connections | grep -v 'SSL enabled'; then fail "grif_app connected without TLS (above)"; fi
  echo "PostgreSQL serves TLS; grif_app's connections, the app's included, use it:"
  grif_app_connections | grep -v 'application_name=psql' | tail -1 | sed 's/.*SSL enabled/  SSL enabled/'
else
  # As each login, straight against SQL Server (its own sqlcmd; no shell, so the passwords pass as they are).
  as_login() {
    local user=$1 pw=$2 db=$3 query=$4
    kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$user" -P "$pw" \
      -C -b -h -1 -W -d "$db" -Q "SET NOCOUNT ON; $query"
  }
  [ "$(kubectl -n "$ns" get deploy grif-backend -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="ConnectionStrings__GrifballWebApp")].value}' | grep -o 'User Id=[^;]*')" = "User Id=grif_app" ] \
    || fail "the backend doesn't connect as grif_app"
  as_login grif_app "$app_password" GrifballWebApp "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;" | grep -qx "$count" \
    || fail "grif_app can't read the database"
  if as_login grif_app "$app_password" GrifballWebApp "CREATE TABLE dbo.Nope (Id int);" 2>/dev/null; then fail "grif_app changed the schema"; fi
  if as_login grif_app "$app_password" master "CREATE LOGIN nope WITH PASSWORD = N'Nope!12345678';" 2>/dev/null; then fail "grif_app created a login"; fi
  as_login grif_monitor "$mon_password" master "SELECT COUNT(*) FROM sys.dm_exec_connections;" >/dev/null \
    || fail "grif_monitor can't read server state"
  if as_login grif_monitor "$mon_password" GrifballWebApp "SELECT 1;" 2>/dev/null; then fail "grif_monitor opened the database"; fi
  echo "The backend connects as grif_app, which reads and writes but can't change the schema or the server;"
  echo "grif_monitor reads server state (sql-exporter's metrics) and can't open the database."
fi

step "Upgrade, nothing pending"
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job "$job")
job_logs "$job" > "$work/upgrade.txt"; cat "$work/upgrade.txt"
expect "Up to date." "$work/upgrade.txt"
expect "Nothing to migrate: no backup needed." "$work/upgrade.txt"
expect "Nothing to migrate." "$work/upgrade.txt"
expect_not "BACKUP DATABASE" "$work/upgrade.txt"
expect_not "Backing up" "$work/upgrade.txt"
[ "$(migration_count)" = "$count" ] || fail "migrations changed"
wait_for 60 sh -c "[ \$(kubectl -n $ns get jobs -l app.kubernetes.io/component=migrations -o name | wc -l) = 1 ]" \
  || fail "the previous migration Job is still there"
echo "A new Job checked, found nothing to do, and replaced the previous one."

step "A pending migration: the backend waits; the Job backs up, then applies it"
if [ "$scenario" = postgres ]; then
  latest=$(pg GrifballWebApp 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 1')
  # The one before it; with a single migration, 0 (EF Core's "before the first").
  previous=$(pg GrifballWebApp 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1 DESC OFFSET 1 LIMIT 1')
  previous=${previous:-0}
  bundle=/app/postgres/efbundle
else
  latest=$(sql "SELECT TOP 1 MigrationId FROM GrifballWebApp.dbo.__EFMigrationsHistory ORDER BY MigrationId DESC" | tr -d '\r' | tail -1)
  previous=$(sql "SELECT MigrationId FROM GrifballWebApp.dbo.__EFMigrationsHistory ORDER BY MigrationId DESC OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY" | tr -d '\r' | tail -1)
  bundle=/app/efbundle
fi
image=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].image}')
conn=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="ConnectionStrings__GrifballWebApp")].value}')
db_secret=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="DB_PASSWORD")].valueFrom.secretKeyRef.name}')
db_key=$(kubectl -n "$ns" get "$job" -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="DB_PASSWORD")].valueFrom.secretKeyRef.key}')
echo "Rolling $latest back to $previous with the image's bundle"
# Labelled as the migration Job so the network policies let it reach the database.
kubectl -n "$ns" run rollback --image="$image" --restart=Never --labels=app=grif-migrate --overrides="$(cat <<EOF
{"spec": {"containers": [{"name": "rollback", "image": "$image", "command": ["$bundle", "$previous"],
  "env": [{"name": "DB_PASSWORD", "valueFrom": {"secretKeyRef": {"name": "$db_secret", "key": "$db_key"}}},
          {"name": "ConnectionStrings__GrifballWebApp", "value": "$conn"}]}]}}
EOF
)"
wait_for 300 sh -c "kubectl -n $ns get pod rollback -o jsonpath='{.status.phase}' | grep -q Succeeded" \
  || { kubectl -n "$ns" logs rollback >&2 || true; fail "rollback failed"; }
kubectl -n "$ns" delete pod rollback --wait=true >/dev/null
[ "$(migration_count)" = $(( count - 1 )) ] || fail "rollback didn't remove $latest"
restart_backend
# All of them with PostgreSQL's single migration rolled back: the list starts with the oldest.
expect_waiting "Waiting for the migration Job to apply: "
wait_logs | grep -F "Waiting for the migration Job to apply: " | grep -qF "$latest" || fail "the backend isn't waiting for $latest"
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job "$job")
job_logs "$job" > "$work/pending.txt"; cat "$work/pending.txt"
expect "Pending migrations:" "$work/pending.txt"
expect "$latest" "$work/pending.txt"
expect "Applying migration '$latest'" "$work/pending.txt"
[ "$(migration_count)" = "$count" ] || fail "$latest not applied again"
if [ "$scenario" = postgres ]; then
  expect "Backed up and verified: /backup/$ns/GrifballWebApp_" "$work/pending.txt"
  dump=$(sed -n "s#^Backed up and verified: ##p" "$work/pending.txt")
  backups "pg_restore --list '$dump' | grep -q 'TABLE DATA public __EFMigrationsHistory'" || fail "$dump isn't a dump pg_restore reads"
else
  expect "BACKUP DATABASE successfully processed" "$work/pending.txt"
  kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "ls /var/opt/mssql/backup/$ns/GrifballWebApp_*.bak" || fail "backup file missing"
fi
wait_for 120 backend_waited || fail "the backend didn't start after the Job"
echo "The Job backed up and applied $latest; the backend then started."

step "Database dropped: the backend waits; the Job recreates it, nothing to back up"
if [ "$scenario" = postgres ]; then
  pg postgres 'DROP DATABASE "GrifballWebApp" WITH (FORCE)'
else
  sql "ALTER DATABASE GrifballWebApp SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE GrifballWebApp;"
fi
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

step "Preview reset: the database has a migration the image lacks, none of master's (a PR's own, regenerated)"
# The image's master lists (CI's, .github/scripts/master-migrations.sh) don't have this ID: as if an
# earlier build of the PR had applied it and the PR then regenerated it under another name.
own=29990101000000_PreviewOwnMigration
if [ "$scenario" = postgres ]; then
  pg GrifballWebApp "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('$own', '10.0.0'); CREATE TABLE public.e2e_preview (v int)"
  marker_left() { [ "$(pg GrifballWebApp "SELECT count(*) FROM information_schema.tables WHERE table_name = 'e2e_preview'")" = 1 ]; }
else
  sql "INSERT INTO GrifballWebApp.dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'$own', N'10.0.0'); CREATE TABLE GrifballWebApp.dbo.E2ePreview (V int);"
  marker_left() { [ "$(sql "SELECT CASE WHEN OBJECT_ID(N'GrifballWebApp.dbo.E2ePreview') IS NULL THEN 0 ELSE 1 END" | tr -d '\r' | tail -1)" = 1 ]; }
fi
[ "$(migration_count)" = $(( count + 1 )) ] || fail "$own wasn't recorded"
# Not a preview (grif-test, staging, prod): no reset step, the database left as it is.
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job "$job")
job_logs "$job" > "$work/not-preview.txt"; cat "$work/not-preview.txt"
expect_not "--- preview-reset" "$work/not-preview.txt"
expect "Nothing to migrate." "$work/not-preview.txt"
marker_left || fail "the database changed without a preview"
[ "$(migration_count)" = $(( count + 1 )) ] || fail "the history changed without a preview"
echo "Not a preview: no reset step; the database kept $own and its data."
# A preview (backend.config.Preview.Enabled): reset, then migrated from scratch.
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}" --set backend.config.Preview.Enabled=true
job=$(wait_job "$job")
job_logs "$job" > "$work/preview.txt"; cat "$work/preview.txt"
expect "--- preview-reset" "$work/preview.txt"
expect "none of them master's: $own" "$work/preview.txt"
expect "Dropped GrifballWebApp: it starts again as a new preview's." "$work/preview.txt"
expect "No database to back up." "$work/preview.txt"
if marker_left; then fail "the preview's database wasn't reset"; fi
[ "$(migration_count)" = "$count" ] || fail "the reset database doesn't have exactly the image's $count migrations"
echo "A preview: the database was dropped and migrated again, with the image's $count migrations and not $own."
# Again, with nothing the image lacks: nothing to reset.
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}" --set backend.config.Preview.Enabled=true
job=$(wait_job "$job")
job_logs "$job" > "$work/preview-again.txt"; cat "$work/preview-again.txt"
expect "has no migration this build lacks: nothing to reset." "$work/preview-again.txt"
[ "$(migration_count)" = "$count" ] || fail "migrations changed"
# Back to the release's own values for the steps below.
helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}"
job=$(wait_job "$job")

# Run the scheduled backup CronJob now, as job/$1; prints the file it wrote.
backup_now() {
  kubectl -n "$ns" create job "$1" --from=cronjob/grif-backup >/dev/null
  wait_for 300 sh -c "kubectl -n $ns get job $1 -o jsonpath='{.status.succeeded}' | grep -q 1" \
    || { kubectl -n "$ns" logs "job/$1" >&2 || true; fail "the scheduled backup didn't succeed"; }
  kubectl -n "$ns" logs "job/$1" > "$work/$1.txt"
  sed -n "s#^Backed up and verified: ##p" "$work/$1.txt"
}

if [ "$scenario" = postgres ]; then
  step "Scheduled backup: a verified pg_dump, named like the Job's, on the backup volume"
  sched=$(backup_now backup-now); cat "$work/backup-now.txt"
  expect "PostgreSQL at postgres:5432 is ready." "$work/backup-now.txt"
  expect_not ": not found" "$work/backup-now.txt"
  expect "Backed up and verified: /backup/$ns/GrifballWebApp_" "$work/backup-now.txt"
  backups "pg_restore --list '$sched' | grep -q 'TABLE DATA public __EFMigrationsHistory'" || fail "$sched isn't a dump pg_restore reads"
  echo "Scheduled backup written and verified: $sched"

  step "Backup retention: the newest backup kept, older ones deleted, anything else left alone"
  dir=/backup/$ns
  # Older dumps (copies of a real one), and files retention must never touch: one named like a
  # backup but not a pg_dump archive, a hand-named one, a half-written one.
  backups "cd $dir && cp '$sched' GrifballWebApp_20260101_000000.dump && cp '$sched' GrifballWebApp_20260102_000000.dump \
    && echo junk > GrifballWebApp_20200101_000000.dump && cp '$sched' GrifballWebApp_manual.dump \
    && cp '$sched' GrifballWebApp_20200101_000000.dump.partial"
  # The chart's own dumps there: named like one, and not the junk file.
  before=$(( $(backups "ls $dir | grep -cE '^GrifballWebApp_[0-9]{8}_[0-9]{6}\.dump$'") - 1 ))
  kubectl -n "$ns" create job retention-now --from=cronjob/grif-backup-retention
  wait_for 300 sh -c "kubectl -n $ns get job retention-now -o jsonpath='{.status.succeeded}' | grep -q 1" \
    || { kubectl -n "$ns" logs job/retention-now >&2 || true; fail "the retention job didn't succeed"; }
  kubectl -n "$ns" logs job/retention-now | tee "$work/retention.txt"
  left=$(backups "ls $dir")
  echo "$left"
  echo "$left" | grep -qxF "${sched##*/}" || fail "the newest backup was deleted"
  for f in GrifballWebApp_20200101_000000.dump GrifballWebApp_manual.dump GrifballWebApp_20200101_000000.dump.partial; do
    echo "$left" | grep -qxF "$f" || fail "$f, not one of the chart's backups, was deleted"
  done
  [ "$(echo "$left" | wc -l)" = 4 ] || fail "expected the newest backup and the 3 other files left"
  expect "Deleted $(( before - 1 )) backup(s)" "$work/retention.txt"
  echo "Kept the newest backup and the other files; deleted the other $(( before - 1 ))."

  step "Restore: a second release, on its first deploy, restores the first's newest dump"
  pg GrifballWebApp "CREATE TABLE public.e2e_marker (v text); INSERT INTO public.e2e_marker VALUES ('restored')"
  marked=$(backup_now backup-marked)
  [ -n "$marked" ] || fail "no backup of the marked database"
  # The second release's restore folder: a volume of its own, holding the marked dump, a junk file
  # named like a newer one, and a hand-named one.
  kubectl create namespace "$restore_ns"
  kubectl -n "$restore_ns" create secret generic grif-secrets --from-literal=POSTGRES_PASSWORD="$password" \
    --from-literal=DiscordClientId=0 --from-literal=DiscordClientSecret=e2e --from-literal=DiscordToken=e2e
  kubectl -n "$restore_ns" apply -f - <<EOF
apiVersion: v1
kind: PersistentVolumeClaim
metadata: { name: restore-source }
spec:
  accessModes: [ReadWriteOnce]
  resources: { requests: { storage: 1Gi } }
---
apiVersion: v1
kind: Pod
metadata: { name: fill }
spec:
  securityContext: { runAsUser: 1654, runAsGroup: 1654, fsGroup: 1654 }
  containers:
    - name: fill
      image: docker.io/library/busybox:1.38
      command: ["sleep", "600"]
      volumeMounts: [{ name: source, mountPath: /source }]
  volumes:
    - name: source
      persistentVolumeClaim: { claimName: restore-source }
EOF
  kubectl -n "$restore_ns" wait --for=condition=Ready pod/fill --timeout=120s
  # Out through a local file (not a pipe from one kubectl exec to another, which can end early on
  # SIGPIPE), and in as base64 in the exec's arguments: stdin into an exec loses data past a few
  # 32 KiB frames (a 291 KB dump arrived as 64-196 KB, every try, 2026-10-11), while arguments
  # travel in the request itself. Both ends' checksums are compared with the original's.
  sum=$(backups "md5sum '$marked'" | awk '{print $1}')
  backups "cat '$marked'" > "$work/marked.dump"
  [ "$(md5sum < "$work/marked.dump" | awk '{print $1}')" = "$sum" ] || fail "${marked##*/} came out of the backup volume damaged"
  target="/source/grif-test/${marked##*/}"
  kubectl -n "$restore_ns" exec fill -- sh -c "mkdir -p /source/grif-test && rm -f '$target.b64'"
  base64 -w0 "$work/marked.dump" | fold -w 100000 | while read -r chunk || [ -n "$chunk" ]; do
    kubectl -n "$restore_ns" exec fill -- sh -c 'printf %s "$2" >> "$1"' _ "$target.b64" "$chunk" </dev/null
  done
  kubectl -n "$restore_ns" exec fill -- sh -c "base64 -d '$target.b64' > '$target' && rm '$target.b64'"
  [ "$(kubectl -n "$restore_ns" exec fill -- md5sum "$target" | awk '{print $1}')" = "$sum" ] \
    || fail "${marked##*/} didn't reach the restore volume whole"
  kubectl -n "$restore_ns" exec fill -- sh -c "cd /source/grif-test && echo junk > GrifballWebApp_29991231_235959.dump && cp '${marked##*/}' GrifballWebApp_manual.dump"
  kubectl -n "$restore_ns" delete pod fill --wait=true
  helm install grif "$chart" -n "$restore_ns" "${images[@]}" -f - <<EOF
database: { provider: postgres }
postgres: { persistence: { size: 1Gi } }
backend:
  config: { BaseUrl: "https://grifball.example", Discord: { DraftChannel: "1" } }
  secretConfig: { "Discord:ClientId": DiscordClientId, "Discord:ClientSecret": DiscordClientSecret, "Discord:Token": DiscordToken }
migrations:
  backup: { volume: { size: 1Gi } }
  restore:
    enabled: true
    directory: /restore/grif-test
    volume: { persistentVolumeClaim: { claimName: restore-source, readOnly: true } }
EOF
  # The functions above work in $ns.
  first_ns=$ns ns=$restore_ns
  job=$(wait_job)
  job_logs "$job" > "$work/restore.txt"; cat "$work/restore.txt"
  expect "Restoring /restore/grif-test/${marked##*/}" "$work/restore.txt"
  expect "No database to back up." "$work/restore.txt"
  [ "$(pg GrifballWebApp 'SELECT v FROM public.e2e_marker')" = restored ] || fail "the marker didn't come back"
  [ "$(migration_count)" = "$count" ] || fail "the restored database doesn't have all $count migrations"
  wait_for 300 backend_waited || fail "the backend didn't see the restored database up to date"
  echo "The second release restored ${marked##*/} (not the newer junk or the hand-named file), marker"
  echo "included, with all $count migrations; its backend then started."

  step "Restore into a database made beforehand with no tables: the dump goes into it, in one transaction"
  pg postgres 'DROP DATABASE "GrifballWebApp" WITH (FORCE)'
  pg postgres 'CREATE DATABASE "GrifballWebApp"'
  helm upgrade grif "$chart" -n "$restore_ns" "${images[@]}" --reuse-values
  job=$(wait_job "$job")
  job_logs "$job" > "$work/restore-empty.txt"; cat "$work/restore-empty.txt"
  expect "Database GrifballWebApp exists with no tables: a new database all the same." "$work/restore-empty.txt"
  expect "Into the existing, empty GrifballWebApp, in one transaction." "$work/restore-empty.txt"
  expect "No database to back up." "$work/restore-empty.txt"
  [ "$(pg GrifballWebApp 'SELECT v FROM public.e2e_marker')" = restored ] || fail "the marker didn't come back into the empty database"
  [ "$(migration_count)" = "$count" ] || fail "the database restored into doesn't have all $count migrations"
  echo "The empty database made beforehand took the dump, marker included, with all $count migrations."
  ns=$first_ns
else
  step "Scheduled backup: a verified copy-only backup, named like the Job's, in SQL Server's history"
  sched=$(backup_now backup-now); cat "$work/backup-now.txt"
  expect "Database GrifballWebApp is ready." "$work/backup-now.txt"
  expect_not ": not found" "$work/backup-now.txt"
  expect "Backed up and verified: /var/opt/mssql/backup/$ns/GrifballWebApp_" "$work/backup-now.txt"
  [ "$(sql "SET NOCOUNT ON; SELECT COUNT(*) FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON m.media_set_id = b.media_set_id WHERE m.physical_device_name = N'$sched' AND b.is_copy_only = 1 AND b.has_backup_checksums = 1" | tr -d '[:space:]')" = 1 ] \
    || fail "$sched isn't a copy-only, checksummed backup in msdb"
  echo "Scheduled backup written and verified: $sched"

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

  step "Restore: the database dropped, a deploy with restore on brings back the newest backup"
  sql "CREATE TABLE GrifballWebApp.dbo.E2eMarker (V nvarchar(20)); INSERT INTO GrifballWebApp.dbo.E2eMarker VALUES (N'restored');"
  marked=$(backup_now backup-marked)
  [ -n "$marked" ] || fail "no backup of the marked database"
  # A hand-named file that sorts above it.
  kubectl -n "$sql_ns" exec "deploy/$sql_deploy" -- sh -c "cp '$marked' $dir/GrifballWebApp_zz_manual.bak"
  sql "ALTER DATABASE GrifballWebApp SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE GrifballWebApp;"
  # The external server's database made again beforehand, empty, as a managed server's often is: a
  # first deploy all the same.
  if [ "$scenario" = external ]; then sql "CREATE DATABASE GrifballWebApp;"; fi
  # The backup folder is the restore folder: on SQL Server's side already.
  helm upgrade grif "$chart" -n "$ns" -f "$work/values.yaml" "${images[@]}" \
    --set migrations.restore.enabled=true --set "migrations.restore.directory=$dir"
  job=$(wait_job "$job")
  job_logs "$job" > "$work/restore.txt"; cat "$work/restore.txt"
  if [ "$scenario" = external ]; then expect "Database GrifballWebApp exists with no tables: a new database all the same." "$work/restore.txt"; fi
  grep "^Restoring " "$work/restore.txt" | grep -qF "${marked##*/}" || fail "didn't restore ${marked##*/}"
  expect "No database to back up." "$work/restore.txt"
  [ "$(sql "SELECT V FROM GrifballWebApp.dbo.E2eMarker" | tr -d '\r' | tail -1)" = restored ] || fail "the marker didn't come back"
  [ "$(migration_count)" = "$count" ] || fail "the restored database doesn't have all $count migrations"
  as_login grif_app "$app_password" GrifballWebApp "SELECT V FROM dbo.E2eMarker;" | grep -qx restored \
    || fail "grif_app can't read the restored database"
  echo "Restored ${marked##*/} (not the hand-named file), marker included, with all $count migrations;"
  echo "grif_app reads it."
fi

if [ "$scenario" != minimal ]; then
  # The first release's database, where the converted one goes, and what it's checked with:
  #   bundled:  the chart's SQL Server into a release on the chart's PostgreSQL;
  #   postgres: the chart's PostgreSQL into a release on the chart's SQL Server;
  #   external: the external SQL Server into another database on it, made beforehand with no tables
  #             (as a managed server's often is), for a release with database.host.
  case "$scenario" in
    bundled)
      from=SqlServer to=postgres from_pod=grif-mssql from_port=1433 target_db=GrifballWebApp
      source_cs="Server=sqlserver.$ns.svc.cluster.local,1433;Database=GrifballWebApp;User Id=sa;Password=$password;Encrypt=True;TrustServerCertificate=True" ;;
    postgres)
      from=Postgres to=sqlserver from_pod=grif-postgres from_port=5432 target_db=GrifballWebApp
      source_cs="Host=postgres.$ns.svc.cluster.local;Port=5432;Database=GrifballWebApp;Username=postgres;Password=$password;SSL Mode=Prefer;GSS Encryption Mode=Disable" ;;
    external)
      from=SqlServer to=sqlserver target_db=GrifballWebAppConverted
      source_cs="Server=mssql.$ext_ns.svc.cluster.local,1433;Database=GrifballWebApp;User Id=sa;Password=$password;Encrypt=True;TrustServerCertificate=True" ;;
  esac
  step "Convert: a release on $to ($scenario), on its first deploy, copies the first's database"
  # The marker table isn't the app's: the conversion copies only the app's tables, and refuses a
  # source with others. A seeded row changed instead, which gives it history too.
  if [ "$scenario" = postgres ]; then
    pg GrifballWebApp "DROP TABLE IF EXISTS public.e2e_marker; UPDATE \"Other\".\"Regions\" SET \"RegionName\" = 'Converted É' WHERE \"RegionID\" = (SELECT min(\"RegionID\") FROM \"Other\".\"Regions\")"
    history=$(pg GrifballWebApp 'SELECT count(*) FROM "Other"."RegionsHistory"')
  else
    sql "DROP TABLE IF EXISTS GrifballWebApp.dbo.E2eMarker; UPDATE GrifballWebApp.Other.Regions SET RegionName = N'Converted É' WHERE RegionID = (SELECT MIN(RegionID) FROM GrifballWebApp.Other.Regions);"
    history=$(sql "SELECT COUNT(*) FROM GrifballWebApp.Other.RegionsHistory" | tr -d '\r' | tail -1)
  fi
  [ "$history" -ge 1 ] || fail "the changed region left no history in the source"
  convert_ns=grif-e2e-convert
  kubectl create namespace "$convert_ns"
  kubectl -n "$convert_ns" create secret generic grif-secrets \
    --from-literal=SA_PASSWORD="$password" --from-literal=POSTGRES_PASSWORD="$password" \
    --from-literal=DiscordClientId=0 --from-literal=DiscordClientSecret=e2e --from-literal=DiscordToken=e2e \
    --from-literal=SOURCE_DATABASE="$source_cs"
  if [ "$scenario" = external ]; then
    sql "CREATE DATABASE $target_db;"
    # The external server has no policies; the release reaches it as database.host.
    target_values="mssql: { enabled: false }
database: { host: mssql.$ext_ns.svc.cluster.local, port: 1433, user: sa, name: $target_db, password: { secretName: grif-secrets, key: SA_PASSWORD } }
migrations:
  backup: { directory: /var/opt/mssql/backup/$convert_ns }"
  else
    # The first release's server admits the new release's migration Job (policies add up).
    kubectl -n "$ns" apply -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata: { name: e2e-convert-source }
spec:
  podSelector: { matchLabels: { app: $from_pod } }
  policyTypes: [Ingress]
  ingress:
    - from:
        - namespaceSelector: { matchLabels: { kubernetes.io/metadata.name: $convert_ns } }
          podSelector: { matchLabels: { app: grif-migrate } }
      ports: [{ protocol: TCP, port: $from_port }]
EOF
    target_values="database: { provider: $to }
mssql:
  acceptEula: true
  resources: { requests: { memory: 1Gi, cpu: 100m }, limits: { memory: 2Gi } }
  persistence: { size: 2Gi }
  backup: { size: 1Gi }
postgres: { persistence: { size: 1Gi } }
migrations:
  backup: { volume: { size: 1Gi } }
networkPolicy:
  migrations:
    extraEgress:
      - to:
          - namespaceSelector: { matchLabels: { kubernetes.io/metadata.name: $ns } }
            podSelector: { matchLabels: { app: $from_pod } }
        ports: [{ protocol: TCP, port: $from_port }]"
  fi
  printf '%s\n' "$target_values" > "$work/convert-values.yaml"
  helm install grif "$chart" -n "$convert_ns" "${images[@]}" -f "$work/convert-values.yaml" -f - <<EOF
backend:
  config: { BaseUrl: "https://grifball.example", Discord: { DraftChannel: "1" } }
  secretConfig: { "Discord:ClientId": DiscordClientId, "Discord:ClientSecret": DiscordClientSecret, "Discord:Token": DiscordToken }
migrations:
  convert:
    enabled: true
    source: { provider: $from, connectionString: { secretName: grif-secrets, key: SOURCE_DATABASE } }
EOF
  first_ns=$ns ns=$convert_ns
  job=$(wait_job)
  job_logs "$job" > "$work/convert.txt"; cat "$work/convert.txt"
  if [ "$scenario" = external ]; then expect "Database $target_db exists with no tables: a new database all the same." "$work/convert.txt"; fi
  expect "Converting a $from database into a new" "$work/convert.txt"
  expect "and checked them" "$work/convert.txt"
  expect "Converted. Up to date." "$work/convert.txt"
  expect "No database to back up." "$work/convert.txt"
  expect "Nothing to migrate." "$work/convert.txt"
  case "$scenario" in
    bundled)
      region=$(pg "$target_db" "SELECT \"RegionName\" FROM \"Other\".\"Regions\" WHERE \"RegionName\" = 'converted é'")
      converted_history=$(pg "$target_db" 'SELECT count(*) FROM "Other"."RegionsHistory"') ;;
    *)
      # The converted database's SQL Server: the release's own, or the external one.
      if [ "$scenario" = postgres ]; then sql_ns=$convert_ns sql_deploy=grif-mssql pw_var=SA_PASSWORD; fi
      region=$(sql "SELECT RegionName FROM $target_db.Other.Regions WHERE RegionName = N'converted é'" | tr -d '\r' | tail -1)
      converted_history=$(sql "SELECT COUNT(*) FROM $target_db.Other.RegionsHistory" | tr -d '\r' | tail -1) ;;
  esac
  [ "$region" = "Converted É" ] || fail "the changed region didn't come across (got '$region')"
  [ "$converted_history" = "$history" ] || fail "the converted database has $converted_history region history rows, the source $history"
  wait_for 300 backend_waited || fail "the backend didn't see the converted database up to date"
  echo "The $to release converted the $from database on its first deploy, into $target_db: the changed"
  echo "region, found case-insensitively, and its $history history row(s); its backend then started."
  ns=$first_ns
fi

step "PASS ($scenario)"
