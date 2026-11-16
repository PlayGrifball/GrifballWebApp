// The chart's dashboard end to end: every panel's query through a real Grafana's query API (/api/ds/query), against
// the Prometheus, Loki and Tempo the release's telemetry reaches (e2e/dashboard.sh starts them). So it covers the
// chart's rendering, Grafana loading the dashboard, every PromQL, LogQL and TraceQL query running against the real
// thing, and the metrics' real names and labels.
//
//   node charts/grifballwebapp/e2e/dashboard-grafana.mjs [--url http://localhost:3000] --namespace grif-dashboard [--timeout 240]
//
// Fails on a query Grafana or a datasource rejects, and on a REQUIRED panel with no data: the ones a release with some
// traffic always fills. The rest need what CI can't make (visitors' browsers, chats, Halo, jobs that run hourly, errors)
// and only have to run. Prints a table of rows per panel.
const args = Object.fromEntries(process.argv.slice(2).reduce((a, v, i, all) => (v.startsWith('--') ? [...a, [v.slice(2), all[i + 1]]] : a), []));
const url = args.url ?? 'http://localhost:3000';
const namespace = args.namespace;
const timeout = Number(args.timeout ?? 240) * 1000;
if (!namespace) throw new Error('--namespace: the release whose telemetry the panels show');

export const REQUIRED = new Set([
  'Requests', 'Server errors', 'p95 latency', 'Unhealthy checks', 'Live connections', 'Exceptions',
  'Requests by route', 'Requests by status', 'p95 latency by route',
  'EF Core', 'Compiled query cache',
  'Server rendering', 'Frontend memory',
  'CPU', 'Memory', 'GC heap and pauses', 'Thread pool queue',
]);

async function call(path, body) {
  const response = await fetch(`${url}${path}`, {
    method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json' }, body: body && JSON.stringify(body),
    signal: AbortSignal.timeout(30000),
  });
  const text = await response.text();
  let json;
  try { json = JSON.parse(text); } catch { json = { message: text }; }
  return { ok: response.ok, status: response.status, json };
}

/** Data points across a /api/ds/query result's frames. */
const rows = result => (result?.frames ?? []).reduce((n, f) => n + (f.data?.values?.[0]?.length ?? 0), 0);

/** A panel target as Grafana's backend takes it: the datasource variables and $namespace filled in. */
function query(target) {
  const type = target.datasource.type;
  const fill = s => s?.replaceAll('$namespace', namespace);
  const q = { ...target, datasource: { type, uid: type }, maxDataPoints: 100, intervalMs: 15000 };
  if (type === 'tempo') return { ...q, query: fill(target.query) };
  return { ...q, expr: fill(target.expr) };
}

async function check() {
  const failures = [];
  const report = ['| Panel | Query | Datasource | Rows |', '| --- | --- | --- | ---: |'];
  for (const uid of ['prometheus', 'loki', 'tempo']) {
    const health = await call(`/api/datasources/uid/${uid}/health`);
    if (health.json.status !== 'OK') failures.push(`datasource ${uid}: ${JSON.stringify(health.json).slice(0, 300)}`);
  }
  if (failures.length) return { failures, report };
  const loaded = await call('/api/dashboards/uid/grifballwebapp');
  if (!loaded.ok) return { failures: [`dashboard grifballwebapp not loaded: ${loaded.status} ${JSON.stringify(loaded.json)}`], report };
  const dashboard = loaded.json.dashboard;
  const env = dashboard.templating.list.find(v => v.name === 'namespace')?.current?.value;
  if (env !== namespace) failures.push(`the dashboard opens on ${env}, not this release's ${namespace}`);
  const panels = dashboard.panels.flatMap(p => [p, ...(p.panels ?? [])]).filter(p => p.targets?.length);
  for (const panel of panels) {
    let count = 0;
    for (const target of panel.targets) {
      const { ok, status, json } = await call('/api/ds/query', { queries: [query(target)], from: 'now-15m', to: 'now' });
      const result = json.results?.[target.refId];
      const error = result?.error ?? (!ok && `${status} ${json.message}`);
      const n = rows(result);
      count += n;
      report.push(`| ${panel.title} | ${target.refId} | ${target.datasource.type} | ${n} |`);
      if (error) failures.push(`${panel.title} (${target.refId}, ${target.datasource.type}): ${error}`);
    }
    if (count === 0 && REQUIRED.has(panel.title)) failures.push(`${panel.title}: no data`);
  }
  for (const title of REQUIRED)
    if (!panels.some(p => p.title === title)) failures.push(`required panel "${title}" isn't on the dashboard`);
  return { failures, report, panels: panels.length };
}

const deadline = Date.now() + timeout;
let outcome;
while (true) {
  try {
    outcome = await check();
  } catch (error) {
    outcome = { failures: [`Grafana not answering: ${error.message}`], report: [] };
  }
  if (!outcome.failures.length || Date.now() > deadline) break;
  console.log(`waiting on: ${outcome.failures.join('; ')}`);
  await new Promise(r => setTimeout(r, 10000));
}
console.log(outcome.report.join('\n'));
if (outcome.failures.length) {
  console.log(`\nFAILED:\n  ${outcome.failures.join('\n  ')}`);
  process.exit(1);
}
console.log(`\nOK: ${outcome.panels} panels queried through Grafana (Prometheus, Loki, Tempo), all ${REQUIRED.size} required ones with data`);
