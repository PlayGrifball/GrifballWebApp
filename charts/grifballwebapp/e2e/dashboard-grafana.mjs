// The chart's dashboard end to end: every panel's query through a real Grafana's query API (/api/ds/query), against
// the Prometheus, Loki and Tempo the release's telemetry reaches (e2e/dashboard.sh starts them). So it covers the
// chart's rendering, Grafana loading the dashboard, every PromQL, LogQL and TraceQL query running against the real
// thing, and the metrics' real names and labels.
//
//   node charts/grifballwebapp/e2e/dashboard-grafana.mjs [--url http://localhost:3000] --namespace grif-dashboard [--timeout 240]
//
// Fails on a query Grafana or a datasource rejects, and on a REQUIRED panel (dashboard-checks.mjs) without real data:
// its queries run again without their zero fill (`or vector(0)`), which would answer with no series at all, and each
// series must carry the labels its legend shows (the pod). Prints a table of data points per panel.
import { REQUIRED, points, requiredQuery, seriesFailures } from './dashboard-checks.mjs';

const args = Object.fromEntries(process.argv.slice(2).reduce((a, v, i, all) => (v.startsWith('--') ? [...a, [v.slice(2), all[i + 1]]] : a), []));
const url = args.url ?? 'http://localhost:3000';
const namespace = args.namespace;
const timeout = Number(args.timeout ?? 240) * 1000;
if (!namespace) throw new Error('--namespace: the release whose telemetry the panels show');

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

/** A panel target as Grafana's backend takes it: the datasource variables and $namespace filled in. */
function query(target) {
  const type = target.datasource.type;
  const fill = s => s?.replaceAll('$namespace', namespace);
  const q = { ...target, datasource: { type, uid: type }, maxDataPoints: 100, intervalMs: 15000 };
  if (type === 'tempo') return { ...q, query: fill(target.query) };
  return { ...q, expr: fill(target.expr) };
}

/** Datasources whose health check isn't OK. */
async function unhealthy() {
  const failures = [];
  for (const uid of ['prometheus', 'loki', 'tempo']) {
    const health = await call(`/api/datasources/uid/${uid}/health`);
    if (health.json.status !== 'OK') failures.push(`datasource ${uid}: ${JSON.stringify(health.json).slice(0, 300)}`);
  }
  return failures;
}

/** Runs one target through Grafana: its result and its error, if any. */
async function run(target) {
  const { ok, status, json } = await call('/api/ds/query', { queries: [query(target)], from: 'now-15m', to: 'now' });
  const result = json.results?.[target.refId];
  return { result, error: result?.error ?? (!ok && `${status} ${json.message}`) };
}

/**
 * A required target as it's checked (requiredQuery: no zero fill), run again when that differs: its data points, and
 * failures for an error or a series without a label its legend shows.
 */
async function checkRequired(title, target, ran) {
  const checked = target.expr && requiredQuery(target.expr) !== target.expr ? await run({ ...target, expr: requiredQuery(target.expr) }) : ran;
  if (checked.error) return { n: 0, failures: checked === ran ? [] : [`${title} (${target.refId} without its zero fill): ${checked.error}`] };
  return { n: points(checked.result), failures: seriesFailures(target, checked.result).map(f => `${title} ${f}`), refilled: checked !== ran };
}

/** Runs one panel's queries: rows for the report and failures. A required panel's data is counted as checkRequired checks it. */
async function runPanel(panel) {
  const report = [], failures = [];
  const required = REQUIRED.has(panel.title);
  let count = 0;
  for (const target of panel.targets) {
    const ran = await run(target);
    if (ran.error) failures.push(`${panel.title} (${target.refId}, ${target.datasource.type}): ${ran.error}`);
    const real = required ? await checkRequired(panel.title, target, ran) : { n: points(ran.result), failures: [] };
    failures.push(...real.failures);
    count += real.n;
    report.push(`| ${panel.title} | ${target.refId} | ${target.datasource.type} | ${points(ran.result)}${real.refilled ? ` (${real.n} without the zero fill)` : ''} |`);
  }
  if (count === 0 && required) failures.push(`${panel.title}: no data`);
  return { report, failures };
}

async function check() {
  const report = ['| Panel | Query | Datasource | Data points |', '| --- | --- | --- | ---: |'];
  const failures = await unhealthy();
  if (failures.length) return { failures, report };
  const loaded = await call('/api/dashboards/uid/grifballwebapp');
  if (!loaded.ok) return { failures: [`dashboard grifballwebapp not loaded: ${loaded.status} ${JSON.stringify(loaded.json)}`], report };
  const dashboard = loaded.json.dashboard;
  const env = dashboard.templating.list.find(v => v.name === 'namespace')?.current?.value;
  if (env !== namespace) failures.push(`the dashboard opens on ${env}, not this release's ${namespace}`);
  const panels = dashboard.panels.flatMap(p => [p, ...(p.panels ?? [])]).filter(p => p.targets?.length);
  for (const panel of panels) {
    const one = await runPanel(panel);
    report.push(...one.report);
    failures.push(...one.failures);
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
