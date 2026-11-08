// Writes browser/build-manifest.json beside the built files (the Dockerfile runs it after ng build):
// the sha256 of every file the site serves to browsers, and of every inline script the build can put
// in a page. CI attests the manifest (docker-publish.yml), and verify-site.mjs checks a live site
// against it: whatever a browser is sent has to be in here.
//
//   files          browser/<path> -> sha256 (hex), every file but the manifest itself
//   server         server/<path> -> sha256, the server's own bundle (src/server.ts and the pages it renders)
//   inlineScripts  sha256 of each inline <script> in the built index pages, and of Angular's event
//                  dispatch contract, which the server inlines into the pages it renders
//
// Paths sort the same everywhere (code point order), so the same build writes the same bytes.
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, existsSync, writeFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const manifestName = 'build-manifest.json';

const sha256 = data => createHash('sha256').update(data).digest('hex');
const byPath = (a, b) => (a < b ? -1 : a > b ? 1 : 0);

function hashTree(root, skip) {
  const out = {};
  const walk = dir => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else {
        const path = relative(root, full).split(sep).join('/');
        if (path !== skip) out[path] = sha256(readFileSync(full));
      }
    }
  };
  walk(root);
  return Object.fromEntries(Object.entries(out).sort(([a], [b]) => byPath(a, b)));
}

// Inline (no src) scripts that run: JSON data blocks (ng-state) don't.
export function inlineScripts(html) {
  const scripts = [];
  for (const [, attributes, body] of html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)) {
    if (/\bsrc\s*=/i.test(attributes)) continue;
    if (/\btype\s*=\s*["']?application\/(?:ld\+)?json/i.test(attributes)) continue;
    scripts.push(body);
  }
  return scripts;
}

// Run (not imported by verify-site.mjs): write the manifest.
if (import.meta.url === `file://${process.argv[1]}`) {
  const dist = process.argv[2] ?? 'dist/grifballwebapp.client';
  const browser = join(dist, 'browser');
  const server = join(dist, 'server');
  const pages = [join(browser, 'index.csr.html'), join(server, 'index.server.html'), join(browser, 'index.html')]
    .filter(existsSync);
  const inline = new Set(pages.flatMap(page => inlineScripts(readFileSync(page, 'utf8'))).map(sha256));
  const contract = 'node_modules/@angular/core/event-dispatch-contract.min.js';
  if (existsSync(contract)) inline.add(sha256(readFileSync(contract, 'utf8')));

  const manifest = {
    files: hashTree(browser, manifestName),
    server: existsSync(server) ? hashTree(server) : {},
    inlineScripts: [...inline].sort(byPath),
  };
  writeFileSync(join(browser, manifestName), JSON.stringify(manifest, null, 2) + '\n');
  console.log(`${manifestName}: ${Object.keys(manifest.files).length} browser files, ${Object.keys(manifest.server).length} server files, ${manifest.inlineScripts.length} inline scripts`);
}
