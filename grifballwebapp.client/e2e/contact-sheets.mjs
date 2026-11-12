// Puts the layout test's screenshots side by side, one contact sheet per page (screenshots/<page>.png), so a PR's look
// can be reviewed at a glance. The page as it opens (screenshots/<page>/<n>-<device>-<width>x<height>.png, from
// e2e/layout.spec.ts) comes first: computers on the first row, tablets on the second, phones on the third. Then each
// state a click reaches on it, and each variant of it (screenshots/<page>/<state>/..., e2e/testPages.mts's OVERLAYS and
// the layout test's pages with a `sheet`), one row each, smaller. CI uploads each sheet unzipped, which GitHub shows in
// the browser.
import { chromium } from '@playwright/test';
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

const dir = 'screenshots';
// Every screenshot is scaled to this height, so desktops and phones line up; a state's, smaller, all on one row
const HEIGHT = 360;
const STATE_HEIGHT = 240;

/** The PNGs in `folder`, in the test's order (the number at the front). */
async function shotsIn(folder) {
  const files = (await readdir(folder)).filter(f => f.endsWith('.png'));
  return files.sort((a, b) => Number.parseInt(a) - Number.parseInt(b));
}

/** A screenshot with its caption: "5-phone-landscape-820x300.png" -> "Phone, landscape · 820×300". */
async function figure(folder, file) {
  const [, kind, orientation, size] = file.match(/^\d+-([a-z]+)(?:-([a-z]+))?-(\d+x\d+)\.png$/);
  const turned = orientation ? `, ${orientation}` : '';
  const caption = `${kind[0].toUpperCase()}${kind.slice(1)}${turned} · ${size.replace('x', '×')}`;
  const data = (await readFile(join(folder, file))).toString('base64');
  return { kind, html: `<figure><img src="data:image/png;base64,${data}"><figcaption>${caption}</figcaption></figure>` };
}

const pages = (await readdir(dir, { withFileTypes: true })).filter(d => d.isDirectory()).map(d => d.name).sort();
const browser = await chromium.launch({ executablePath: process.env['PW_CHROMIUM_PATH'] || undefined });
const page = await browser.newPage({ viewport: { width: 1800, height: 600 } });

for (const name of pages) {
  const folder = join(dir, name);
  // The page as it opens: a row per kind of device
  const rows = new Map();
  const files = await shotsIn(folder);
  for (const file of files) {
    const { kind, html } = await figure(folder, file);
    const row = kind === 'desktop' || kind === 'laptop' ? 'computer' : kind;
    rows.set(row, [...(rows.get(row) ?? []), html]);
  }
  const sections = [...rows.values()].map(row => `<div class="row">${row.join('')}</div>`);
  // Its states and variants: a heading and one row each
  let count = files.length;
  const states = (await readdir(folder, { withFileTypes: true })).filter(d => d.isDirectory()).map(d => d.name).sort();
  for (const state of states) {
    const stateFiles = await shotsIn(join(folder, state));
    count += stateFiles.length;
    const figures = await Promise.all(stateFiles.map(file => figure(join(folder, state), file)));
    sections.push(`<h2>${state}</h2><div class="row row--state">${figures.map(f => f.html).join('')}</div>`);
  }
  await page.setContent(`<!doctype html>
    <style>
      body { margin: 0; padding: 24px; background: #222; color: #eee; font: 16px system-ui, sans-serif; width: fit-content; }
      h1 { margin: 0 0 16px; font-size: 24px; }
      h2 { margin: 16px 0 -8px; font-size: 20px; color: #9cf; }
      main { display: grid; gap: 24px; }
      .row { display: flex; gap: 16px; }
      figure { margin: 0; }
      img { display: block; height: ${HEIGHT}px; outline: 1px solid #555; }
      .row--state img { height: ${STATE_HEIGHT}px; }
      figcaption { margin-top: 6px; color: #ddd; font-size: 18px; }
    </style>
    <h1>${name}</h1>
    <main>${sections.join('')}</main>`);
  await page.locator('body').screenshot({ path: join(dir, `${name}.png`) });
  console.log(`${dir}/${name}.png: ${count} screenshots, ${states.length} states`);
}

await browser.close();
