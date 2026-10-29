// Puts the layout test's screenshots (screenshots/<page>/<n>-<device>-<width>x<height>.png, from
// e2e/layout.spec.ts) side by side, one contact sheet per page (screenshots/<page>.png), so a PR's
// look can be reviewed at a glance: computers on the first row, tablets on the second, phones on
// the third. CI uploads each sheet unzipped, which GitHub shows in the browser.
import { chromium } from '@playwright/test';
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

const dir = 'screenshots';
// Every screenshot is scaled to this height, so desktops and phones line up
const HEIGHT = 360;

const pages = (await readdir(dir, { withFileTypes: true })).filter(d => d.isDirectory()).map(d => d.name).sort();
const browser = await chromium.launch({ executablePath: process.env['PW_CHROMIUM_PATH'] || undefined });
const page = await browser.newPage({ viewport: { width: 1800, height: 600 } });

for (const name of pages) {
  const files = (await readdir(join(dir, name))).filter(f => f.endsWith('.png'));
  // In the test's order, the number at the front
  files.sort((a, b) => parseInt(a) - parseInt(b));
  const rows = new Map();
  for (const file of files) {
    // "5-phone-landscape-820x300.png" -> "Phone, landscape · 820×300", on the phones' row
    const [, kind, orientation, size] = file.match(/^\d+-([a-z]+)(?:-([a-z]+))?-(\d+x\d+)\.png$/);
    const caption = `${kind[0].toUpperCase()}${kind.slice(1)}${orientation ? `, ${orientation}` : ''} · ${size.replace('x', '×')}`;
    const row = kind === 'desktop' || kind === 'laptop' ? 'computer' : kind;
    const data = (await readFile(join(dir, name, file))).toString('base64');
    rows.set(row, [...(rows.get(row) ?? []), `<figure><img src="data:image/png;base64,${data}"><figcaption>${caption}</figcaption></figure>`]);
  }
  const shots = [...rows.values()].map(row => `<div class="row">${row.join('')}</div>`);
  await page.setContent(`<!doctype html>
    <style>
      body { margin: 0; padding: 24px; background: #222; color: #eee; font: 16px system-ui, sans-serif; width: fit-content; }
      h1 { margin: 0 0 16px; font-size: 24px; }
      main { display: grid; gap: 24px; }
      .row { display: flex; gap: 16px; }
      figure { margin: 0; }
      img { display: block; height: ${HEIGHT}px; outline: 1px solid #555; }
      figcaption { margin-top: 6px; color: #ddd; font-size: 18px; }
    </style>
    <h1>${name}</h1>
    <main>${shots.join('')}</main>`);
  await page.locator('body').screenshot({ path: join(dir, `${name}.png`) });
  console.log(`${dir}/${name}.png: ${files.length} screenshots`);
}

await browser.close();
