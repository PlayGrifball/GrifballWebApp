// The chart's Grafana dashboard (files/dashboards/grifballwebapp.json), offline:
//   node --test charts/grifballwebapp/tests/*.test.mjs
//   - the JSON is what files/dashboards/grifballwebapp.mjs makes (edit the script, then run it)
//   - it has a uid, unique panel ids, panels inside the 24-column grid; every row open and the whole of it inside the
//     screenshot e2e/dashboard-screenshot.mjs takes
//   - every datasource it names is one of its own variables (a fixed uid would only work in one Grafana)
//   - every grif_* metric it queries is an instrument the app defines (the backend's C#, the frontend's TypeScript), so a
//     renamed metric fails here, not as an empty panel
//   - every Prometheus query parses as PromQL (promtool, from PROMTOOL; skipped without it: CI sets it); Loki and Tempo
//     queries aren't empty (they run for real in the dashboard job: e2e/dashboard.sh)
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import generated from '../files/dashboards/grifballwebapp.mjs';

const chart = path.resolve(import.meta.dirname, '..');
const repo = path.resolve(chart, '../..');
const dashboard = JSON.parse(fs.readFileSync(path.join(chart, 'files/dashboards/grifballwebapp.json'), 'utf8'));

const panels = d => d.panels.flatMap(p => [p, ...(p.panels ?? [])]);
const targets = d => panels(d).flatMap(p => (p.targets ?? []).map(t => ({ panel: p.title, type: t.datasource?.type, target: t })));
const prometheus = d => targets(d).filter(t => t.type === 'prometheus');
/** A query as plain PromQL: the environment, Grafana's interval and the health paths the chart fills in (its default ones). */
const promql = expr => expr.replaceAll('$namespace', 'grif-test').replaceAll('$__rate_interval', '5m')
  .replaceAll('__HEALTH_ROUTES__', '(?i)(?:/health|/health/live|/health/ready)(?:/.*)?');

test('the JSON is what the generator makes', () => {
  assert.deepEqual(dashboard, JSON.parse(JSON.stringify(generated)), 'run: node charts/grifballwebapp/files/dashboards/grifballwebapp.mjs');
});

test('a uid, unique panel ids, panels inside the grid, the environment the chart fills in', () => {
  assert.equal(dashboard.uid, 'grifballwebapp');
  const ids = panels(dashboard).map(p => p.id);
  assert.equal(new Set(ids).size, ids.length, 'panel ids repeat');
  for (const p of panels(dashboard))
    assert.ok(p.gridPos.x >= 0 && p.gridPos.x + p.gridPos.w <= 24, `${p.title} is outside the 24 columns`);
  assert.equal(dashboard.templating.list.find(v => v.name === 'namespace')?.current.value, '__NAMESPACE__');
});

test('every row open, and the whole dashboard inside the screenshot', () => {
  // e2e/dashboard-screenshot.mjs: a 6000 px viewport, and Grafana only draws panels on screen. A grid unit is 30 px plus
  // an 8 px gap, so 150 units (5700 px) leave room for Grafana's header.
  for (const row of dashboard.panels.filter(p => p.type === 'row')) assert.equal(row.collapsed, false, `row ${row.title} is collapsed`);
  const bottom = Math.max(...panels(dashboard).map(p => p.gridPos.y + p.gridPos.h));
  assert.ok(bottom <= 150, `the dashboard is ${bottom} grid units tall: raise the screenshot's viewport`);
});

test('every datasource is one of its variables', () => {
  const variables = new Set(dashboard.templating.list.filter(v => v.type === 'datasource').map(v => `\${${v.name}}`));
  const walk = (o, where) => {
    if (Array.isArray(o)) return o.forEach(x => walk(x, where));
    if (!o || typeof o !== 'object') return;
    if (o.datasource) assert.ok(variables.has(o.datasource.uid), `${where}: datasource ${o.datasource.uid} is not a datasource variable`);
    for (const [k, v] of Object.entries(o)) if (k !== 'datasource') walk(v, o.title ?? where);
  };
  walk(dashboard.panels, 'dashboard');
  walk(dashboard.templating.list.filter(v => v.type !== 'datasource'), 'variables');
});

/** The projects whose sources define the app's grif.* instruments: every GrifballWebApp.* one but the tests, and the client's. */
const sourceDirs = () => [
  ...fs.readdirSync(repo, { withFileTypes: true }).filter(e => e.isDirectory() && /^GrifballWebApp\./.test(e.name) && !e.name.endsWith('.Test')).map(e => e.name),
  'grifballwebapp.client/src',
];

const CREATE = String.raw`[Cc]reate(?:Counter|Histogram|UpDownCounter|Gauge|ObservableCounter|ObservableGauge|ObservableUpDownCounter)(?:<[^>]*>)?`;

/**
 * The grif.* instruments one source file creates, as Prometheus names them (dots to underscores): the names given to a
 * meter's create call, or to a local helper that passes its first parameter on to one (BrowserMetrics' Seconds).
 * Attribute keys (grif.request.kind, grif.tool.outcome) aren't instruments.
 */
const instrumentsIn = source => {
  const helpers = [...source.matchAll(new RegExp(String.raw`\b(\w+)\s*(?:=\s*)?\(\s*(?:string\s+)?(\w+)\b[^)]*\)\s*(?::\s*\w+(?:<[^>]*>)?\s*)?=>\s*[\w.]+\.${CREATE}\(\s*\2\b`, 'g'))].map(m => m[1]);
  const callers = [`\\.${CREATE}`, ...helpers.map(h => `\\b${h}`)].join('|');
  return [...source.matchAll(new RegExp(String.raw`(?:${callers})\(\s*["'](grif\.[a-z0-9_]+(?:\.[a-z0-9_]+)+)["']`, 'g'))].map(m => m[1].replaceAll('.', '_'));
};

/** Instruments: the grif.* ones the backend's C# and the frontend's TypeScript create (searched once). */
let searched;
function appInstruments() {
  if (searched) return searched;
  const instruments = new Set();
  const walk = dir => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const f = path.join(dir, e.name);
      if (e.isDirectory()) { if (!['bin', 'obj', 'node_modules', 'dist', '.angular'].includes(e.name)) walk(f); }
      else if (/\.(cs|ts)$/.test(e.name) && !/\.spec\.ts$|Test/.test(f))
      {
        const source = fs.readFileSync(f, 'utf8');
        if (source.includes('grif.')) for (const i of instrumentsIn(source)) instruments.add(i);
      }
    }
  };
  for (const dir of sourceDirs()) walk(path.join(repo, dir));
  searched = instruments;
  return instruments;
}

test('the instrument search finds instruments the app creates, not attribute keys, in every project', () => {
  assert.deepEqual(instrumentsIn(`
    const requestDuration = meter.createHistogram('grif.frontend.request.duration', REQUEST_DURATION);
    meter.createObservableGauge('grif.frontend.memory.rss', { unit: 'By' })
    span.setAttribute('grif.render.outcome', how);
    export const REQUEST_KIND_ATTRIBUTE = 'grif.request.kind';`), ['grif_frontend_request_duration', 'grif_frontend_memory_rss']);
  assert.deepEqual(instrumentsIn(`
    public const string OutcomeTag = "grif.tool.outcome";
    private readonly Counter<long> _calls = _meter.CreateCounter<long>("grif.tool.calls", "{call}");
    Histogram<double> Seconds(string name, string description) =>
        _meter.CreateHistogram(name, "s", description, advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = SecondsBuckets });
    ["LCP"] = Seconds("grif.browser.lcp", "Largest Contentful Paint"),
    ["CLS"] = _meter.CreateHistogram("grif.browser.cls", null, "Cumulative Layout Shift"),
    activity?.SetTag("grif.tool.error.type", error);`), ['grif_tool_calls', 'grif_browser_lcp', 'grif_browser_cls']);
  for (const project of ['GrifballWebApp.Server', 'GrifballWebApp.Infrastructure', 'GrifballWebApp.Application', 'grifballwebapp.client/src'])
    assert.ok(sourceDirs().includes(project), `${project} isn't searched`);
  const instruments = appInstruments();
  for (const attribute of ['grif_request_kind', 'grif_render_outcome', 'grif_tool_outcome', 'grif_tool_error_type'])
    assert.ok(!instruments.has(attribute), `${attribute} is an attribute key, not an instrument`);
  for (const instrument of ['grif_browser_lcp', 'grif_frontend_render_duration', 'grif_frontend_memory_rss'])
    assert.ok(instruments.has(instrument), `${instrument} not found`);
});

test('every grif_* metric it queries is one the app defines', () => {
  const instruments = appInstruments();
  assert.ok(instruments.size >= 5, `found only ${instruments.size} instruments: the search is broken`);
  const used = new Set(prometheus(dashboard).flatMap(q => [...q.target.expr.matchAll(/\b(grif_[a-z_]+)/g)].map(m => m[1])));
  assert.ok(used.size > 0);
  for (const metric of used) {
    const base = metric.replace(/_(bucket|sum|count)$/, '').replace(/_total$/, '');
    const known = [...instruments].some(i => base === i || base === `${i}_seconds` || base === `${i}_bytes`);
    assert.ok(known, `${metric} is no instrument the app defines`);
  }
});

test('the workflows that run these tests run them when the app\'s instruments change', () => {
  // A pull request's paths filter, as a regex: ** any path, * one segment
  const glob = pattern => new RegExp(`^${pattern.replaceAll('.', '\\.').replaceAll('**', '\0').replaceAll('*', '[^/]*').replaceAll('\0', '.*')}$`);
  const workflows = path.join(repo, '.github/workflows');
  const running = fs.readdirSync(workflows).map(f => [f, fs.readFileSync(path.join(workflows, f), 'utf8')]).filter(([, w]) => w.includes('tests/*.test.mjs'));
  assert.ok(running.length > 0, 'no workflow runs the dashboard tests');
  for (const [file, workflow] of running) {
    const paths = workflow.match(/\n {2}pull_request:\s*\n(?: {4,}.*\n)*? {4}paths:\s*\[([^\]]*)\]/)?.[1];
    if (!paths) continue;  // no paths filter: every pull request
    const patterns = [...paths.matchAll(/"([^"]+)"/g)].map(m => glob(m[1]));
    for (const dir of sourceDirs())
      for (const source of [`${dir}/Telemetry/Metrics.cs`, `${dir}/app/core/telemetry.ts`])
        assert.ok(patterns.some(p => p.test(source)), `${file} doesn't run on a change to ${source}`);
  }
});

test('Loki and Tempo queries are not empty', () => {
  for (const t of targets(dashboard).filter(t => t.type !== 'prometheus'))
    assert.ok(String(t.target.expr ?? t.target.query ?? '').trim(), `${t.panel}: empty ${t.type} query`);
});

test('every Prometheus query parses as PromQL', { skip: !process.env.PROMTOOL && 'PROMTOOL not set (CI sets it)' }, () => {
  // As recording rules, one group each, so promtool names the panel of a query that doesn't parse
  const groups = prometheus(dashboard).map((q, i) => `  - name: "${i} ${q.panel}"\n    rules:\n      - record: q\n        expr: ${JSON.stringify(promql(q.target.expr))}\n`);
  const selector = dashboard.templating.list.find(v => v.name === 'namespace').query.query.match(/\((\w+\{.*\}),/)[1];
  groups.push(`  - name: environment variable\n    rules:\n      - record: q\n        expr: ${JSON.stringify(`count by (k8s_namespace_name) (${selector})`)}\n`);
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'dashboard-')), 'queries.yml');
  fs.writeFileSync(file, `groups:\n${groups.join('')}`);
  const r = spawnSync(process.env.PROMTOOL, ['check', 'rules', file], { encoding: 'utf8' });
  assert.equal(r.status, 0, r.stdout + r.stderr);
});

// The e2e checker's rules (e2e/dashboard-checks.mjs), on made-up results: what makes a panel's data real
const checks = () => import('../e2e/dashboard-checks.mjs');
/** A Grafana /api/ds/query result: one frame per series, with its labels and some points. */
const result = (...series) => ({ frames: series.map(labels => ({ schema: { fields: [{ name: 'Time' }, { name: 'Value', labels }] }, data: { values: [[1, 2, 3], [0, 0, 0]] } })) });

test('a required panel is checked without its zero fill, which data could never be missing from', async () => {
  const { REQUIRED, requiredQuery } = await checks();
  assert.equal(requiredQuery('sum(rate(dotnet_exceptions_total{job=~"x/.+"}[5m])) * 60 or vector(0)'), 'sum(rate(dotnet_exceptions_total{job=~"x/.+"}[5m])) * 60');
  assert.equal(requiredQuery('(sum(rate(a[5m])) or vector(0)) / ((sum(rate(a[5m])) or vector(0)) + (sum(rate(b[5m])) or vector(0)))'),
    '(sum(rate(a[5m]))) / ((sum(rate(a[5m]))) + (sum(rate(b[5m]))))');
  for (const t of prometheus(dashboard).filter(t => REQUIRED.has(t.panel)))
    assert.ok(!/vector\s*\(/.test(requiredQuery(t.target.expr)), `${t.panel}: still a constant once its zero fill is gone`);
});

test('a required per-pod panel needs every series labelled with its pod', async () => {
  const { REQUIRED, seriesFailures } = await checks();
  const cpu = prometheus(dashboard).find(t => t.panel === 'CPU').target;
  assert.deepEqual(seriesFailures(cpu, result({ k8s_pod_name: 'grif-backend-1' }, { k8s_pod_name: 'grif-backend-2' })), []);
  assert.equal(seriesFailures(cpu, result({})).length, 1, 'an unlabelled series passed');
  assert.equal(seriesFailures(cpu, result({ k8s_pod: 'grif-backend-1' })).length, 1, 'a misnamed label passed');
  const perPod = new Set(prometheus(dashboard).filter(t => t.target.legendFormat?.includes('{{k8s_pod_name}}')).map(t => t.panel));
  assert.ok(perPod.size >= 5);
  for (const panel of perPod) assert.ok(REQUIRED.has(panel), `per-pod panel ${panel} isn't required, so its pod label goes unchecked`);
});

test('Server rendering charts the render metric, not every request the frontend served', () => {
  const exprs = prometheus(dashboard).filter(t => t.panel === 'Server rendering').map(t => t.target.expr);
  assert.ok(exprs.length > 0);
  for (const expr of exprs) {
    assert.match(expr, /grif_frontend_render_duration_seconds_/);
    assert.doesNotMatch(expr, /grif_frontend_request_duration/);
  }
});

test('the Environment variable lists a release in any namespace, and nothing but this app', () => {
  // Its query's selector against target_info series: the chart's pods (fixed Deployment names) in namespaces of any name
  const variable = dashboard.templating.list.find(v => v.name === 'namespace');
  const [, metric, selector, label] = variable.query.query.match(/^label_values\((\w+)\{(.*)\},\s*(\w+)\)$/);
  assert.equal(metric, 'target_info');
  assert.equal(label, 'k8s_namespace_name');
  const matchers = [...selector.matchAll(/(\w+)\s*(=~|=|!=|!~)\s*"([^"]*)"/g)].map(([, l, op, v]) => ({ l, op, re: new RegExp(`^(?:${op.length === 1 || op === '!=' ? v.replaceAll(/[.*+?^${}()|[\]\\]/g, '\\$&') : v})$`) }));
  const matches = series => matchers.every(({ l, op, re }) => op.startsWith('!') !== re.test(series[l] ?? ''));
  for (const [ns, pod] of [['grifball', 'grif-backend-6d9f7c5b8-x2x4q'], ['default', 'grif-frontend-5c8d9-abcde'], ['grif-prod', 'grif-worker-7f8-zzzzz'], ['apps', 'grif-discord-5f6-qqqqq']])
    assert.ok(matches({ job: `${ns}/${ns}`, k8s_namespace_name: ns, k8s_pod_name: pod }), `the release in ${ns} isn't listed`);
  for (const pod of ['loki-0', 'grafana-7c9-abcde', 'grif-mssql-0'])
    assert.ok(!matches({ job: 'monitoring/x', k8s_namespace_name: 'monitoring', k8s_pod_name: pod }), `${pod} is listed`);
});

test('Errors and warnings reads the app\'s own OTLP logs by a label Loki indexes, at warning and above', () => {
  // Loki's default index labels for OTLP resource attributes (distributor otlp_config), dots as underscores
  const indexed = ['cloud.availability_zone', 'cloud.region', 'container.name', 'deployment.environment.name', 'k8s.cluster.name', 'k8s.container.name',
    'k8s.cronjob.name', 'k8s.daemonset.name', 'k8s.deployment.name', 'k8s.job.name', 'k8s.namespace.name', 'k8s.pod.name', 'k8s.replicaset.name',
    'k8s.statefulset.name', 'service.instance.id', 'service.name', 'service.namespace'].map(a => a.replaceAll('.', '_'));
  const logs = targets(dashboard).find(t => t.panel === 'Errors and warnings');
  const [, selector, pipeline] = logs.target.expr.match(/^\{([^}]*)\}(.*)$/);
  const labels = [...selector.matchAll(/(\w+)\s*=~?\s*"/g)].map(m => m[1]);
  assert.ok(labels.length > 0);
  for (const label of labels) assert.ok(indexed.includes(label), `${label} isn't a label Loki indexes OTLP logs by`);
  assert.match(logs.target.expr, /\$namespace/);
  // Loki's detected_level, from the OTLP severity (the Telemetry page's level filter reads it too)
  assert.match(pipeline, /detected_level\s*=~\s*"warn\|error\|fatal\|critical"/);
});

test('the health probes left out of the latency panels are the chart\'s paths, filled in when it renders', () => {
  for (const t of prometheus(dashboard).filter(t => t.target.expr.includes('http_route!~')))
    assert.match(t.target.expr, /http_route!~"__HEALTH_ROUTES__"/, `${t.panel} leaves out fixed paths`);
  assert.doesNotMatch(JSON.stringify(dashboard), /\/health/);
});

test('Server errors fills a release without 5xx with 0 from its own requests, not a constant', async () => {
  // A constant fill would come off for the e2e's check and leave the share empty while nothing failed
  const { requiredQuery } = await checks();
  const expr = prometheus(dashboard).find(t => t.panel === 'Server errors').target.expr;
  assert.equal(requiredQuery(expr), expr);
  assert.match(expr, /http_response_status_code=~"5\.\."/);
});
