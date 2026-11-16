// The chart's Grafana dashboard (files/dashboards/grifballwebapp.json), offline:
//   node --test charts/grifballwebapp/tests/*.test.mjs
//   - the JSON is what files/dashboards/grifballwebapp.mjs makes (edit the script, then run it)
//   - it has a uid, unique panel ids, panels inside the 24-column grid
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
/** A query as plain PromQL: the environment and Grafana's interval filled in. */
const promql = expr => expr.replaceAll('$namespace', 'grif-test').replaceAll('$__rate_interval', '5m');

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

test('every grif_* metric it queries is one the app defines', () => {
  // Instruments: "grif.x.y" names in the backend's C# and the frontend's TypeScript, as Prometheus names them
  const instruments = new Set();
  const walk = dir => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const f = path.join(dir, e.name);
      if (e.isDirectory()) { if (!['bin', 'obj', 'node_modules', 'dist', '.angular'].includes(e.name)) walk(f); }
      else if (/\.(cs|ts)$/.test(e.name) && !/\.spec\.ts$|Test/.test(f))
        for (const m of fs.readFileSync(f, 'utf8').matchAll(/["'](grif\.[a-z_]+\.[a-z_.]+)["']/g)) instruments.add(m[1].replaceAll('.', '_'));
    }
  };
  for (const dir of ['GrifballWebApp.Server', 'GrifballWebApp.Infrastructure', 'grifballwebapp.client/src']) walk(path.join(repo, dir));
  assert.ok(instruments.size >= 5, `found only ${instruments.size} instruments: the search is broken`);
  const used = new Set(prometheus(dashboard).flatMap(q => [...q.target.expr.matchAll(/\b(grif_[a-z_]+)/g)].map(m => m[1])));
  assert.ok(used.size > 0);
  for (const metric of used) {
    const base = metric.replace(/_(bucket|sum|count)$/, '').replace(/_total$/, '');
    const known = [...instruments].some(i => base === i || base === `${i}_seconds` || base === `${i}_bytes`);
    assert.ok(known, `${metric} is no instrument the app defines`);
  }
});

test('Loki and Tempo queries are not empty', () => {
  for (const t of targets(dashboard).filter(t => t.type !== 'prometheus'))
    assert.ok(String(t.target.expr ?? t.target.query ?? '').trim(), `${t.panel}: empty ${t.type} query`);
});

test('every Prometheus query parses as PromQL', { skip: !process.env.PROMTOOL && 'PROMTOOL not set (CI sets it)' }, () => {
  // As recording rules, one group each, so promtool names the panel of a query that doesn't parse
  const groups = prometheus(dashboard).map((q, i) => `  - name: "${i} ${q.panel}"\n    rules:\n      - record: q\n        expr: ${JSON.stringify(promql(q.target.expr))}\n`);
  groups.push(`  - name: environment variable\n    rules:\n      - record: q\n        expr: ${JSON.stringify('count by (k8s_namespace_name) (target_info{job=~"grif-.+/.+"})')}\n`);
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'dashboard-')), 'queries.yml');
  fs.writeFileSync(file, `groups:\n${groups.join('')}`);
  const r = spawnSync(process.env.PROMTOOL, ['check', 'rules', file], { encoding: 'utf8' });
  assert.equal(r.status, 0, r.stdout + r.stderr);
});
