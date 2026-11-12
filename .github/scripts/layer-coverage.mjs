#!/usr/bin/env node
// Each layer's unit test coverage, from its own unit test project alone: the Domain's from GrifballWebApp.Domain.Test,
// the Application's from GrifballWebApp.Application.Test, and so on (a project's layer is its name without .Test).
// What another layer's tests happen to run in it doesn't count, so a layer is only as covered as its own tests make it.
//
//   collect <dir> <out.json>: every <dir>/<Project>.Test.cobertura.xml (one per unit test project, as dotnet-tests.yml
//     copies them) to one JSON file: { "<Layer>": { lines, linesCovered, branches, branchesCovered } }. A project whose
//     layer isn't in its report (the architecture tests) is left out.
//   render <current.json> [<base.json>] [<sha>]: the Markdown table, against master's numbers when there are any. Exits 1
//     when a layer master had at the target is now below it: once a layer reaches the target it stays there (a layer
//     still below it only reports).
//
// Env: LAYER_COVERAGE_TARGET, the line coverage every layer should reach (percent, default 70).
import { readFileSync, readdirSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const target = Number(process.env.LAYER_COVERAGE_TARGET ?? 70);

// The attributes of one tag's text (what's between < and >)
function attributes(tag) {
  const result = {};
  for (const match of tag.matchAll(/([\w-]+)="([^"]*)"/g)) {
    result[match[1]] = match[2];
  }
  return result;
}

// One package's numbers from a Cobertura report. A line can be listed more than once (by its class and by its method,
// or by two classes in one file): it counts once, as covered when any listing hit it, with its most covered branches.
export function packageCoverage(xml, name) {
  const lines = new Map();
  let inPackage = false;
  let file = '';
  for (let start = xml.indexOf('<'); start !== -1; start = xml.indexOf('<', start + 1)) {
    const end = xml.indexOf('>', start);
    if (end === -1) break;
    const tag = xml.slice(start + 1, end);
    if (tag.startsWith('package ')) {
      inPackage = attributes(tag).name === name;
    } else if (tag === '/package') {
      inPackage = false;
    } else if (inPackage && tag.startsWith('class ')) {
      file = attributes(tag).filename ?? '';
    } else if (inPackage && tag.startsWith('line ')) {
      const line = attributes(tag);
      const key = `${file}:${line.number}`;
      const branches = /\((\d+)\/(\d+)\)/.exec(line['condition-coverage'] ?? '');
      const seen = lines.get(key) ?? { hit: false, branches: 0, branchesCovered: 0 };
      lines.set(key, {
        hit: seen.hit || Number(line.hits) > 0,
        branches: Math.max(seen.branches, branches ? Number(branches[2]) : 0),
        branchesCovered: Math.max(seen.branchesCovered, branches ? Number(branches[1]) : 0),
      });
    }
  }
  if (lines.size === 0) return null;
  const all = [...lines.values()];
  return {
    lines: all.length,
    linesCovered: all.filter((l) => l.hit).length,
    branches: all.reduce((sum, l) => sum + l.branches, 0),
    branchesCovered: all.reduce((sum, l) => sum + l.branchesCovered, 0),
  };
}

export function collect(dir) {
  const layers = {};
  for (const file of readdirSync(dir).filter((f) => f.endsWith('.Test.cobertura.xml')).sort()) {
    const layer = file.slice(0, -'.Test.cobertura.xml'.length);
    const coverage = packageCoverage(readFileSync(join(dir, file), 'utf8'), layer);
    if (coverage) layers[layer] = coverage;
  }
  return layers;
}

const percent = (covered, total) => (total === 0 ? null : (100 * covered) / total);
const show = (value) => (value === null ? '–' : `${value.toFixed(1)}%`);

function delta(now, before) {
  if (now === null || before === null || before === undefined) return '';
  const change = now - before;
  if (Math.abs(change) < 0.05) return ' (±0)';
  return ` (${change > 0 ? '+' : ''}${change.toFixed(1)})`;
}

export function render(current, base, sha) {
  const out = [
    '### Unit test coverage by layer',
    '',
    `Each layer's coverage from its own unit tests alone (the other layers substituted), ${sha ? `as of ${sha}` : 'this run'}` +
      `${base ? ', change from master in brackets' : ''}. Target: ${target}% of lines.`,
    '',
    '| Layer | Lines | Branches | |',
    '|---|---|---|---|',
  ];
  let below = 0;
  const dropped = [];
  for (const [layer, c] of Object.entries(current).sort(([a], [b]) => a.localeCompare(b))) {
    const lines = percent(c.linesCovered, c.lines);
    const branches = percent(c.branchesCovered, c.branches);
    const was = base?.[layer];
    const linesWas = was ? percent(was.linesCovered, was.lines) : null;
    const branchesWas = was ? percent(was.branchesCovered, was.branches) : null;
    const met = lines !== null && lines >= target;
    if (!met) below++;
    const wasMet = linesWas !== null && linesWas >= target;
    if (wasMet && !met) dropped.push(layer.replace(/^GrifballWebApp\./, ''));
    out.push(
      `| ${layer.replace(/^GrifballWebApp\./, '')} | ${show(lines)}${delta(lines, linesWas)} · ${c.linesCovered}/${c.lines} | ` +
        `${show(branches)}${delta(branches, branchesWas)} | ${met ? '✅' : `${wasMet ? '⛔ was at target on master,' : '❌'} ${Math.ceil((target / 100) * c.lines - c.linesCovered)} lines short`} |`,
    );
  }
  out.push('', below === 0 ? `Every layer is at ${target}% or more.` : `${below} layer(s) below ${target}%.`);
  if (dropped.length) {
    out.push('', `**${dropped.join(', ')} dropped below ${target}%**: a layer at the target on master has to stay there.`);
  }
  return { markdown: out.join('\n'), dropped };
}

const [mode, ...args] = process.argv.slice(2);
if (mode === 'collect') {
  writeFileSync(args[1], `${JSON.stringify(collect(args[0]), null, 2)}\n`);
} else if (mode === 'render') {
  const base = args[1] && existsSync(args[1]) ? JSON.parse(readFileSync(args[1], 'utf8')) : null;
  const { markdown, dropped } = render(JSON.parse(readFileSync(args[0], 'utf8')), base, args[2]);
  console.log(markdown);
  if (dropped.length) process.exitCode = 1;
} else if (mode !== undefined) {
  console.error('Usage: layer-coverage.mjs collect <dir> <out.json> | render <current.json> [<base.json>] [<sha>]');
  process.exit(2);
}
