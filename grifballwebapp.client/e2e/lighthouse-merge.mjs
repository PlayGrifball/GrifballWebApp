// Merges the Markdown tables of e2e/lighthouse-summary.mjs from the jobs CI splits the pages
// between (LIGHTHOUSE_SHARD in lighthouserc.cjs) into one table per title (Mobile, Desktop), its
// rows sorted by page as each job sorts its own.
// Usage: node e2e/lighthouse-merge.mjs <lighthouse.md>...  (prints the merged tables)
import { readFile } from 'node:fs/promises';

// By title ("#### Mobile"): the table's two header lines and its rows
const tables = new Map();
for (const file of process.argv.slice(2)) {
  let table;
  for (const line of (await readFile(file, 'utf8')).split('\n')) {
    if (line.startsWith('#### ')) {
      table = tables.get(line) ?? { head: [], rows: [] };
      tables.set(line, table);
      table.seen = 0;
    } else if (table && line.startsWith('|')) {
      // The column names and the alignment line, once; then the pages
      if (table.seen++ < 2) {
        if (table.head.length < 2) table.head.push(line);
      } else {
        table.rows.push(line);
      }
    }
  }
}
// "| [/season/12](https://...) | ..." or "| /season/12 | ...": the page
const page = row => row.match(/^\| \[?([^\]|]+)/)?.[1].trim() ?? row;
console.log([...tables].map(([title, { head, rows }]) =>
  [title, '', ...head, ...rows.sort((a, b) => page(a).localeCompare(page(b))), ''].join('\n')).join('\n'));
