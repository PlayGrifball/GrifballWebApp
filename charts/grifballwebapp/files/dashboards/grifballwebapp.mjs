// Generates grifballwebapp.json, the chart's Grafana dashboard: node charts/grifballwebapp/files/dashboards/grifballwebapp.mjs
// Built here rather than hand-edited, so its panels share one shape; tests/dashboard.test.mjs fails if the JSON and
// this script disagree. One dashboard for every environment: its queries select the app's series with
// job=~"$namespace/.+" (the collector's job is <service.namespace>/<service.name>), the Environment variable, which
// the chart opens on its release's namespace (__NAMESPACE__, templates/grafana-dashboard.yaml).
import { writeFileSync } from 'node:fs';

const P = { type: 'prometheus', uid: '${datasource}' }, L = { type: 'loki', uid: '${loki}' }, T = { type: 'tempo', uid: '${tempo}' };
const S = 'job=~"$namespace/.+"', R = '$__rate_interval';
const panels = [];
let id = 0, y = 0, x = 0, rowH = 0;

// Lays panels out left to right, wrapping at 24 columns.
const place = (w, h) => { if (x + w > 24) { x = 0; y += rowH; rowH = 0; } const g = { h, w, x, y }; x += w; rowH = Math.max(rowH, h); return g; };
const endRow = () => { y += rowH; x = 0; rowH = 0; };
const row = title => { endRow(); panels.push({ id: ++id, type: 'row', title, collapsed: false, gridPos: { h: 1, w: 24, x: 0, y }, panels: [] }); y += 1; };
const targets = list => list.map(([expr, legend], i) => ({ refId: String.fromCodePoint(65 + i), datasource: P, expr, legendFormat: legend, range: true }));
const ts = (title, description, list, unit = 'short', w = 12, extra = {}) => panels.push({ id: ++id, type: 'timeseries', title, description, datasource: P, gridPos: place(w, 8), targets: targets(list),
  fieldConfig: { defaults: { unit, custom: { drawStyle: 'line', lineWidth: 1, fillOpacity: 10, showPoints: 'never', spanNulls: true }, ...(extra.defaults || {}) }, overrides: extra.overrides || [] },
  options: { legend: { displayMode: 'table', placement: 'right', calcs: ['max', 'lastNotNull'] }, tooltip: { mode: 'multi', sort: 'desc' } } });
const stat = (title, description, expr, unit, steps, decimals) => panels.push({ id: ++id, type: 'stat', title, description, datasource: P, gridPos: place(4, 4), targets: targets([[expr, '']]),
  fieldConfig: { defaults: { unit, decimals, thresholds: { mode: 'absolute', steps } }, overrides: [] },
  options: { reduceOptions: { calcs: ['lastNotNull'], fields: '', values: false }, colorMode: 'background', graphMode: 'area', textMode: 'value' } });
const traces = (title, description, query) => panels.push({ id: ++id, type: 'table', title, description, datasource: T, gridPos: place(12, 10),
  targets: [{ refId: 'A', datasource: T, queryType: 'traceql', query, limit: 20, tableType: 'traces' }], options: { showHeader: true }, fieldConfig: { defaults: {}, overrides: [] } });
const green = [{ color: 'green', value: null }];

row('Overview');
stat('Requests', 'HTTP requests the backend served, per minute (health probes included).', `sum(rate(http_server_request_duration_seconds_count{${S}}[${R}])) * 60`, 'reqpm', green);
stat('Server errors', 'Share of backend requests answered 5xx.', `(sum(rate(http_server_request_duration_seconds_count{${S}, http_response_status_code=~"5.."}[${R}])) or vector(0)) / sum(rate(http_server_request_duration_seconds_count{${S}}[${R}]))`, 'percentunit', [{ color: 'green', value: null }, { color: 'orange', value: 0.01 }, { color: 'red', value: 0.05 }]);
stat('p95 latency', '95th percentile backend request time, health probes left out.', `histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{${S}, http_route!~"/health.*"}[${R}])))`, 's', [{ color: 'green', value: null }, { color: 'orange', value: 1 }, { color: 'red', value: 3 }]);
stat('Unhealthy checks', 'Health checks that ended Degraded or Unhealthy in the last 10 minutes (e.g. halo_infinite: the Halo gateway).', `sum(increase(dotnet_health_check_unhealthy_checks_total{${S}}[10m])) or vector(0)`, 'short', [{ color: 'green', value: null }, { color: 'orange', value: 1 }], 0);
stat('Live connections', 'SignalR connections open now (the site\'s live updates).', `sum(signalr_server_active_connections{${S}}) or vector(0)`, 'short', green, 0);
stat('Exceptions', 'Exceptions thrown in the backend, per minute (caught ones included).', `sum(rate(dotnet_exceptions_total{${S}}[${R}])) * 60 or vector(0)`, 'short', [{ color: 'green', value: null }, { color: 'orange', value: 10 }]);

row('Backend requests');
ts('Requests by route', 'Backend requests per minute, by route (top 10).', [[`topk(10, sum by (http_route) (rate(http_server_request_duration_seconds_count{${S}}[${R}])) * 60)`, '{{http_route}}']], 'reqpm');
ts('Requests by status', 'Backend requests per minute, by status code.', [[`sum by (http_response_status_code) (rate(http_server_request_duration_seconds_count{${S}}[${R}])) * 60`, '{{http_response_status_code}}']], 'reqpm');
ts('p95 latency by route', 'Slowest routes: 95th percentile request time (top 10), health probes left out.', [[`topk(10, histogram_quantile(0.95, sum by (http_route, le) (rate(http_server_request_duration_seconds_bucket{${S}, http_route!~"/health.*"}[${R}]))))`, '{{http_route}}']], 's');
ts('Server errors by route', 'Backend 5xx answers per minute, by route.', [[`sum by (http_route, http_response_status_code) (rate(http_server_request_duration_seconds_count{${S}, http_response_status_code=~"5.."}[${R}])) * 60`, '{{http_route}} ({{http_response_status_code}})']], 'reqpm');

row('Outgoing calls');
ts('Outgoing HTTP by host', 'Calls the backend makes, per minute, by host (the Halo gateway, Discord, Google, ...).', [[`sum by (server_address) (rate(http_client_request_duration_seconds_count{${S}}[${R}])) * 60`, '{{server_address}}']], 'reqpm');
ts('Outgoing p95 by host', '95th percentile time of those calls, by host.', [[`histogram_quantile(0.95, sum by (server_address, le) (rate(http_client_request_duration_seconds_bucket{${S}}[${R}])))`, '{{server_address}}']], 's');
ts('Outgoing errors by host', 'Calls answered 4xx/5xx or failed, per minute, by host and status.', [[`sum by (server_address, http_response_status_code, error_type) (rate(http_client_request_duration_seconds_count{${S}, http_response_status_code=~"[45]..|"}[${R}])) * 60`, '{{server_address}} {{http_response_status_code}}{{error_type}}']], 'reqpm', 24);

row('Database');
ts('Database operations by query', 'Database operations per minute, by query summary (top 10).', [[`topk(10, sum by (db_query_summary) (rate(db_client_operation_duration_seconds_count{${S}}[${R}])) * 60)`, '{{db_query_summary}}']], 'reqpm');
ts('Slowest queries (p95)', '95th percentile time of database operations, by query summary (top 10).', [[`topk(10, histogram_quantile(0.95, sum by (db_query_summary, le) (rate(db_client_operation_duration_seconds_bucket{${S}}[${R}]))))`, '{{db_query_summary}}']], 's');
ts('EF Core', 'Entity Framework queries and SaveChanges per minute, and failures: execution strategy (retried) and optimistic concurrency.', [
  [`sum(rate(microsoft_entityframeworkcore_queries_total{${S}}[${R}])) * 60`, 'queries'], [`sum(rate(microsoft_entityframeworkcore_savechanges_total{${S}}[${R}])) * 60`, 'SaveChanges'],
  [`sum(rate(microsoft_entityframeworkcore_execution_strategy_operation_failures_total{${S}}[${R}])) * 60`, 'execution failures'], [`sum(rate(microsoft_entityframeworkcore_optimistic_concurrency_failures_total{${S}}[${R}])) * 60`, 'concurrency failures']], 'reqpm');
ts('Compiled query cache', 'EF Core compiled query cache hit ratio: misses mean queries built each time.', [[`(sum(rate(microsoft_entityframeworkcore_compiled_query_cache_hits_total{${S}}[${R}])) or vector(0)) / ((sum(rate(microsoft_entityframeworkcore_compiled_query_cache_hits_total{${S}}[${R}])) or vector(0)) + (sum(rate(microsoft_entityframeworkcore_compiled_query_cache_misses_total{${S}}[${R}])) or vector(0)))`, 'hit ratio']], 'percentunit', 12, { defaults: { min: 0, max: 1 } });

row('Jobs');
ts('Job runs by result', 'Quartz job runs per hour, by job and result.', [[`sum by (quartz_job_name, quartz_job_result) (increase(quartz_job_execution_duration_seconds_count{${S}}[1h]))`, '{{quartz_job_name}} ({{quartz_job_result}})']]);
ts('Job run time (p95)', '95th percentile time of a job run, by job.', [[`histogram_quantile(0.95, sum by (quartz_job_name, le) (rate(quartz_job_execution_duration_seconds_bucket{${S}}[${R}])))`, '{{quartz_job_name}}']], 's');
ts('Jobs running', 'Job runs in progress now, by job.', [[`sum by (quartz_job_name) (quartz_job_execution_active{${S}})`, '{{quartz_job_name}}']]);
ts('Scheduler lock wait (p95)', 'How long schedulers waited for the job store lock (several pods share it).', [[`histogram_quantile(0.95, sum by (le) (rate(quartz_jobstore_lock_wait_duration_seconds_bucket{${S}}[${R}])))`, 'lock wait']], 's');

row('Frontend and browsers');
ts('Server rendering', 'Requests the frontend (server-side rendering) served, per minute, and their 95th percentile time.', [
  [`sum(rate(grif_frontend_request_duration_seconds_count{${S}}[${R}])) * 60`, 'requests/min'], [`histogram_quantile(0.95, sum by (le) (rate(grif_frontend_request_duration_seconds_bucket{${S}}[${R}])))`, 'p95 (s)']]);
ts('Frontend memory', 'Resident memory of the frontend pods.', [[`sum by (k8s_pod_name) (grif_frontend_memory_rss_bytes{${S}})`, '{{k8s_pod_name}}']], 'bytes');
ts('Web vitals (p75)', 'What visitors\' browsers measured, 75th percentile: largest contentful paint, interaction to next paint, first contentful paint and time to first byte (through telemetry-gate).',
  [['lcp', 'LCP'], ['inp', 'INP'], ['fcp', 'FCP'], ['ttfb', 'TTFB']].map(([m, l]) => [`histogram_quantile(0.75, sum by (le) (rate(grif_browser_${m}_seconds_bucket{${S}}[${R}])))`, l]), 's');
ts('Layout shift (p75)', 'Cumulative layout shift visitors\' browsers measured, 75th percentile (under 0.1 is good).', [[`histogram_quantile(0.75, sum by (le) (rate(grif_browser_cls_bucket{${S}}[${R}])))`, 'CLS']], 'none');

row('Chats (AI model calls)');
ts('Model calls', 'Calls the site chat and DMs make to their model, per hour, by provider and model.', [[`sum by (gen_ai_provider_name, gen_ai_request_model) (increase(gen_ai_client_operation_duration_seconds_count{${S}}[1h]))`, '{{gen_ai_provider_name}} {{gen_ai_request_model}}']]);
ts('Tokens', 'Tokens used per hour, by type (input, output) and model.', [[`sum by (gen_ai_token_type, gen_ai_request_model) (increase(gen_ai_client_token_usage_sum{${S}}[1h]))`, '{{gen_ai_token_type}} {{gen_ai_request_model}}']]);
ts('Answer time (p95)', 'How long a model call took in all (p95), and until its first streamed chunk (p95), by model.', [
  [`histogram_quantile(0.95, sum by (gen_ai_request_model, le) (rate(gen_ai_client_operation_duration_seconds_bucket{${S}}[${R}])))`, 'whole {{gen_ai_request_model}}'],
  [`histogram_quantile(0.95, sum by (gen_ai_request_model, le) (rate(gen_ai_client_operation_time_to_first_chunk_seconds_bucket{${S}}[${R}])))`, 'first chunk {{gen_ai_request_model}}']], 's');
ts('Model errors', 'Model calls that failed, per hour, by error type.', [[`sum by (error_type, gen_ai_request_model) (increase(gen_ai_client_operation_duration_seconds_count{${S}, error_type!=""}[1h]))`, '{{gen_ai_request_model}} {{error_type}}']]);

row('Runtime');
ts('CPU', 'Backend and worker CPU use, in cores, by pod.', [[`sum by (k8s_pod_name) (rate(process_cpu_time_seconds_total{${S}}[${R}]))`, '{{k8s_pod_name}}']], 'short', 8);
ts('Memory', 'Working set by pod.', [[`sum by (k8s_pod_name) (dotnet_process_memory_working_set_bytes{${S}})`, '{{k8s_pod_name}}']], 'bytes', 8);
ts('GC heap and pauses', 'Managed heap after the last collection (left) and time paused for GC per minute (right).', [
  [`sum by (k8s_pod_name) (dotnet_gc_last_collection_heap_size_bytes{${S}})`, 'heap {{k8s_pod_name}}'], [`sum by (k8s_pod_name) (rate(dotnet_gc_pause_time_seconds_total{${S}}[${R}])) * 60`, 'paused {{k8s_pod_name}}']], 'bytes', 8,
  { overrides: [{ matcher: { id: 'byRegexp', options: 'paused.*' }, properties: [{ id: 'unit', value: 's' }, { id: 'custom.axisPlacement', value: 'right' }] }] });
ts('Thread pool queue', 'Work items waiting for a thread pool thread: a lasting queue means starvation.', [[`sum by (k8s_pod_name) (dotnet_thread_pool_queue_length_total{${S}})`, '{{k8s_pod_name}}']], 'short', 8);
ts('Lock contention', 'Monitor lock contentions per minute.', [[`sum by (k8s_pod_name) (rate(dotnet_monitor_lock_contentions_total{${S}}[${R}])) * 60`, '{{k8s_pod_name}}']], 'short', 8);
ts('Exceptions by type', 'Exceptions thrown per minute, by type.', [[`topk(10, sum by (error_type) (rate(dotnet_exceptions_total{${S}}[${R}])) * 60)`, '{{error_type}}']], 'short', 8);

row('Logs and traces');
panels.push({ id: ++id, type: 'logs', title: 'Errors and warnings', description: 'Backend log lines at Warning and above (Serilog compact JSON: @l is set only for non-Information levels).', datasource: L, gridPos: place(24, 10),
  targets: [{ refId: 'A', datasource: L, expr: '{namespace="$namespace"} |~ "\\"@l\\":\\"(Warning|Error|Fatal)\\""', queryType: 'range' }],
  options: { showTime: true, wrapLogMessage: true, sortOrder: 'Descending', enableLogDetails: true, dedupStrategy: 'none', prettifyLogMessage: false } });
traces('Failed requests', 'Traces with an error, newest first (Tempo). Open one to see where it failed.', '{resource.service.namespace="$namespace" && status=error}');
traces('Slow requests', 'Server spans that took over a second (Tempo).', '{resource.service.namespace="$namespace" && kind=server && duration>1s}');
endRow();

const dashboard = {
  title: 'Grifball web app', uid: 'grifballwebapp', tags: ['grif', 'grifballwebapp'], timezone: 'browser', schemaVersion: 39, version: 1, editable: true,
  description: 'GrifballWebApp, one environment at a time: requests, outgoing calls, database, jobs, frontend and browsers, chats, runtime, logs and traces. Series pushed over OTLP.',
  time: { from: 'now-6h', to: 'now' }, refresh: '1m', graphTooltip: 1,
  templating: { list: [
    { name: 'datasource', label: 'Prometheus', type: 'datasource', query: 'prometheus', current: {}, hide: 0 },
    { name: 'loki', label: 'Loki', type: 'datasource', query: 'loki', current: {}, hide: 0 },
    { name: 'tempo', label: 'Tempo', type: 'datasource', query: 'tempo', current: {}, hide: 0 },
    { name: 'namespace', label: 'Environment', type: 'query', datasource: P, query: { query: 'label_values(target_info{job=~"grif-.+/.+"}, k8s_namespace_name)', refId: 'ns' },
      definition: 'label_values(target_info{job=~"grif-.+/.+"}, k8s_namespace_name)', refresh: 2, sort: 1, current: { text: '__NAMESPACE__', value: '__NAMESPACE__' }, options: [], hide: 0 },
  ] },
  annotations: { list: [] }, links: [], panels,
};
export default dashboard;
if (import.meta.url === `file://${process.argv[1]}`)
  writeFileSync(new URL('./grifballwebapp.json', import.meta.url), JSON.stringify(dashboard, null, 2) + '\n');
