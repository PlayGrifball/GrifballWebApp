#!/usr/bin/env bash
# The chart's Grafana dashboard against the app itself, in the e2e cluster (k3d "e2e"): an OpenTelemetry Collector,
# Prometheus (its OTLP receiver), Loki and Tempo run beside the cluster on its Docker network, as in production; the
# release (ci/sqlite-values.yaml) sends its telemetry there; requests make some traffic; then Grafana loads the dashboard
# the chart rendered and dashboard-grafana.mjs runs every panel's query through Grafana's query API.
#
#   BACKEND_IMAGE=grif-backend:e2e FRONTEND_IMAGE=grif-frontend:e2e charts/grifballwebapp/e2e/dashboard.sh
#
# The collector passes the telemetry on as the chart sent it: its identity (service.namespace, k8s.namespace.name,
# k8s.pod.name) is the chart's, so a chart that stopped sending it fails here. Prometheus keeps the namespace and the pod
# on every series, as a collector's prometheus exporter with resource_to_telemetry_conversion does. Versions are
# production's.
set -euo pipefail

chart="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
backend_image="${BACKEND_IMAGE:-grif-backend:e2e}"
frontend_image="${FRONTEND_IMAGE:-grif-frontend:e2e}"
# Not grif-*: the Environment variable has to list a release in a namespace of any name
ns=grifball-dashboard
net=k3d-e2e
work="$(mktemp -d)"
COLLECTOR_IMAGE=docker.io/otel/opentelemetry-collector-contrib:0.163.2@sha256:7f22d2a6748110ffb6a59bc0df448bccbf501cdb667d610ede4c61d3863d5849
PROMETHEUS_IMAGE=quay.io/prometheus/prometheus:v3.15.1-distroless@sha256:451a845bae1d51622b41af1780eb2ad911bbb79a9855bc8e9bb67fabbc03f934
LOKI_IMAGE=docker.io/grafana/loki:3.7.9@sha256:0db216b630ae1f348a1584c67f3cead8b4c1fc839169a39501db664744158e28
TEMPO_IMAGE=docker.io/grafana/tempo:3.1.1@sha256:64e2fbd56d9c2b3412a4b430a58ea59e91a670b1e75f920a0c702213ad145044
GRAFANA_IMAGE=docker.io/grafana/grafana:13.2.4-distroless@sha256:ae3d3add290adbe6a88412229f5d76fd7bb82743050881698642d3d62f277e35
containers=(collector prometheus loki tempo grafana)

cleanup() {
  local rc=$?
  if [[ "$rc" != 0 ]]; then
    for c in "${containers[@]}"; do echo "::group::$c logs"; docker logs --tail 60 "$c" 2>&1 || true; echo "::endgroup::"; done
    echo "::group::pods"; kubectl -n "$ns" get pods -o wide || true; echo "::endgroup::"
    echo "::group::events"; kubectl -n "$ns" get events --sort-by=.lastTimestamp 2>&1 | tail -40 || true; echo "::endgroup::"
    echo "::group::backend logs"; kubectl -n "$ns" logs deploy/grif-backend --tail 80 2>&1 || true; echo "::endgroup::"
  fi
  docker rm -f "${containers[@]}" > /dev/null 2>&1 || true
}
trap cleanup EXIT

cat > "$work/prometheus.yml" <<'EOF'
global:
  scrape_interval: 15s
otlp:
  promote_resource_attributes: [k8s.namespace.name, k8s.pod.name]
EOF
cat > "$work/tempo.yaml" <<'EOF'
server:
  http_listen_port: 3200
distributor:
  receivers:
    otlp:
      protocols:
        grpc:
          endpoint: 0.0.0.0:4317
storage:
  trace:
    backend: local
    local:
      path: /var/tempo/blocks
    wal:
      path: /var/tempo/wal
EOF
cat > "$work/collector.yaml" <<EOF
receivers:
  otlp:
    protocols:
      grpc: { endpoint: 0.0.0.0:4317 }
      http: { endpoint: 0.0.0.0:4318 }
processors:
  batch: { timeout: 2s }
exporters:
  otlphttp/prometheus: { metrics_endpoint: http://prometheus:9090/api/v1/otlp/v1/metrics }
  otlphttp/loki: { logs_endpoint: http://loki:3100/otlp/v1/logs }
  otlp/tempo: { endpoint: tempo:4317, tls: { insecure: true } }
service:
  pipelines:
    metrics: { receivers: [otlp], processors: [batch], exporters: [otlphttp/prometheus] }
    logs: { receivers: [otlp], processors: [batch], exporters: [otlphttp/loki] }
    traces: { receivers: [otlp], processors: [batch], exporters: [otlp/tempo] }
EOF
chmod -R a+rX "$work"

docker run -d --name prometheus --network "$net" -v "$work/prometheus.yml:/etc/prometheus/prometheus.yml:ro" "$PROMETHEUS_IMAGE" \
  --config.file=/etc/prometheus/prometheus.yml --web.enable-otlp-receiver > /dev/null
docker run -d --name loki --network "$net" "$LOKI_IMAGE" -config.file=/etc/loki/local-config.yaml > /dev/null
# Tempo runs as a user of its own (10001): the tmpfs is open to it, or it can't make its blocks directory
docker run -d --name tempo --network "$net" --tmpfs /var/tempo:mode=1777 -v "$work/tempo.yaml:/etc/tempo.yaml:ro" "$TEMPO_IMAGE" -config.file=/etc/tempo.yaml > /dev/null
docker run -d --name collector --network "$net" -v "$work/collector.yaml:/etc/otelcol/config.yaml:ro" "$COLLECTOR_IMAGE" --config=/etc/otelcol/config.yaml > /dev/null
collector=$(docker inspect -f "{{(index .NetworkSettings.Networks \"$net\").IPAddress}}" collector)
echo "Collector at $collector"
# Plain OTLP to the collector beside the cluster, on the e2e Docker network only. The backend in the app's preview mode
# without a Discord bot token (no secretConfig): it gets ready without Discord, which a single backend pod otherwise
# can't (run.sh's stand-in token gets a 401 and stops it). Its memory check's limit at 1 MB: the check reports Degraded
# (it never gates readiness), so the health check metrics count a check that isn't healthy and the backend logs a
# warning for it, which the Unhealthy checks and Errors and warnings panels must show
cat > "$work/telemetry-values.yaml" <<EOF
backend:
  config: { Preview: { Enabled: true }, HealthChecks: { MaxAllocatedMegabytes: 1 } }
  secretConfig: null
  telemetry: { enabled: true, endpoint: "http://$collector:4317", metricExportInterval: 5000 }
frontend:
  telemetry: { enabled: true, endpoint: "http://$collector:4318" }
grafanaDashboard: { enabled: true }
EOF

helm install grif "$chart" -n "$ns" --create-namespace -f "$chart/ci/sqlite-values.yaml" -f "$work/telemetry-values.yaml" \
  --set "backend.image.repository=${backend_image%:*}" --set "backend.image.tag=${backend_image##*:}" \
  --set "frontend.image.repository=${frontend_image%:*}" --set "frontend.image.tag=${frontend_image##*:}" \
  --set backend.image.pullPolicy=IfNotPresent --set frontend.image.pullPolicy=IfNotPresent
kubectl -n "$ns" rollout status deploy/grif-backend --timeout=600s
kubectl -n "$ns" rollout status deploy/grif-frontend --timeout=300s

# Traffic, all through the frontend (the backend takes requests only from it, NetworkPolicy): pages (server rendering,
# which calls the API) and the API it proxies, a 404 among them, the health check that runs every check (the memory one
# Degraded: a warning), and a SignalR connection to the queue's hub (negotiate, then a few seconds of server-sent
# events) for Live connections
kubectl -n "$ns" run traffic --image=docker.io/curlimages/curl:8.16.0 --restart=Never --command -- sleep 900
kubectl -n "$ns" wait --for=condition=Ready pod/traffic --timeout=120s
for i in $(seq 1 6); do
  for url in http://grif-frontend/ http://grif-frontend/seasons http://grif-frontend/api/seasons http://grif-frontend/api/does-not-exist http://grif-frontend/api/health; do
    kubectl -n "$ns" exec traffic -- curl -s -o /dev/null -w "%{http_code} $url\n" --max-time 20 "$url" || true
  done
  token=$(kubectl -n "$ns" exec traffic -- curl -s -X POST --max-time 20 "http://grif-frontend/hub/QueueHub/negotiate?negotiateVersion=1" \
    | sed -n 's/.*"connectionToken":"\([^"]*\)".*/\1/p' || true)
  echo "SignalR connection: ${token:-none}"
  if [[ -n "$token" ]]; then
    kubectl -n "$ns" exec traffic -- curl -s -o /dev/null -H 'Accept: text/event-stream' --max-time 5 "http://grif-frontend/hub/QueueHub?id=$token" || true
  fi
  sleep 5
done

# Grafana: the three datasources at the uids the checker uses, and the dashboard as the chart rendered it
mkdir -p "$work/datasources" "$work/providers" "$work/dashboards"
cat > "$work/datasources/all.yaml" <<'EOF'
apiVersion: 1
datasources:
  - { name: Prometheus, type: prometheus, uid: prometheus, url: http://prometheus:9090, access: proxy, isDefault: true }
  - { name: Loki, type: loki, uid: loki, url: http://loki:3100, access: proxy }
  - { name: Tempo, type: tempo, uid: tempo, url: http://tempo:3200, access: proxy }
EOF
cat > "$work/providers/chart.yaml" <<'EOF'
apiVersion: 1
providers:
  - { name: chart, type: file, options: { path: /var/lib/grafana/dashboards } }
EOF
kubectl -n "$ns" get configmap grif-dashboard -o jsonpath='{.data.grifballwebapp\.json}' > "$work/dashboards/grifballwebapp.json"
chmod -R a+rX "$work"
docker run -d --name grafana --network "$net" -p 13000:3000 \
  -e GF_AUTH_ANONYMOUS_ENABLED=true -e GF_AUTH_ANONYMOUS_ORG_ROLE=Admin -e GF_AUTH_DISABLE_LOGIN_FORM=true \
  -v "$work/datasources:/etc/grafana/provisioning/datasources:ro" -v "$work/providers:/etc/grafana/provisioning/dashboards:ro" \
  -v "$work/dashboards:/var/lib/grafana/dashboards:ro" "$GRAFANA_IMAGE" > /dev/null

node "$chart/e2e/dashboard-grafana.mjs" --url http://localhost:13000 --namespace "$ns" --timeout "${TIMEOUT:-240}"

# A picture of it (dashboard-screenshot.mjs) at $SCREENSHOT, when set (CI uploads it). In Microsoft's Playwright image at
# the client's @playwright/test version, which the image's browser must match; the playwright package isn't in the image,
# so it's installed first, at that version. The picture is handed back to this user.
if [[ -n "${SCREENSHOT:-}" ]]; then
  repo="$(cd "$chart/../.." && pwd)"
  playwright=$(node -p "require('$repo/grifballwebapp.client/package.json').devDependencies['@playwright/test']")
  mkdir -p "$(dirname "$SCREENSHOT")"
  out="$(cd "$(dirname "$SCREENSHOT")" && pwd)"
  name="$(basename "$SCREENSHOT")"
  docker run --rm --network host --ipc=host --init -v "$chart/e2e:/e2e:ro" -v "$out:/out" \
    "mcr.microsoft.com/playwright:v$playwright-noble" sh -c "
      set -e; mkdir /shot; cd /shot; cp /e2e/dashboard-screenshot.mjs .
      npm install --silent --no-save --no-audit --no-fund playwright@$playwright
      node dashboard-screenshot.mjs http://localhost:13000 $ns /out/$name
      chown $(id -u):$(id -g) /out/$name"
  echo "Screenshot: $SCREENSHOT"
fi
