// Turns a Lighthouse CI run (.lighthouseci/lhr-*.json, plus links.json after an upload to
// temporary public storage) into a Markdown table of the category scores, one row per page.
// Usage: node e2e/lighthouse-summary.mjs <title>  (prints the table; on stderr, a GitHub warning
// annotation for each page whose performance is under 90, which lighthouserc.cjs does not fail on)
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

const dir = '.lighthouseci';
const title = process.argv[2] ?? 'Lighthouse';
const categories = [['performance', 'Performance'], ['accessibility', 'Accessibility'], ['best-practices', 'Best practices'], ['seo', 'SEO']];

const links = await readFile(join(dir, 'links.json'), 'utf8').then(JSON.parse, () => ({}));
const reports = [];
for (const file of (await readdir(dir)).filter(f => /^lhr-.*\.json$/.test(f))) {
  reports.push(JSON.parse(await readFile(join(dir, file), 'utf8')));
}
reports.sort((a, b) => a.requestedUrl.localeCompare(b.requestedUrl));

const score = (lhr, id) => {
  const value = lhr.categories[id]?.score;
  if (value == null) return '–';
  const points = Math.round(value * 100);
  return `${points >= 90 ? '🟢' : points >= 50 ? '🟠' : '🔴'} ${points}`;
};
for (const lhr of reports) {
  const performance = lhr.categories.performance?.score;
  if (performance != null && performance < 0.9)
    console.error(`::warning title=Lighthouse performance::${title}: ${new URL(lhr.requestedUrl).pathname}${new URL(lhr.requestedUrl).search} scores ${Math.round(performance * 100)} (under 90)`);
}
const rows = reports.map(lhr => {
  const url = new URL(lhr.requestedUrl);
  const page = url.pathname + url.search;
  const link = links[lhr.requestedUrl];
  return `| ${link ? `[${page}](${link})` : page} | ${categories.map(([id]) => score(lhr, id)).join(' | ')} |`;
});
console.log([
  `#### ${title}`,
  '',
  `| Page | ${categories.map(([, name]) => name).join(' | ')} |`,
  `|---|${categories.map(() => '---:').join('|')}|`,
  ...rows,
  '',
].join('\n'));
