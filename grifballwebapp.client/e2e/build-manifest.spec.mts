import { createHash } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { checkPage, loaderSources } from '../verify-site.mjs';
import { PUBLIC, SITE, VISITOR } from './testPages.mts';

// The build's manifest (build-manifest.mjs, browser/build-manifest.json) against the site the build serves
// (e2e/serve.mjs): what verify-site.mjs checks on the live site, here on every public page, so a change that
// makes the server send a script the manifest doesn't know fails here rather than on the live site.

interface Manifest { files: Record<string, string>, inlineScripts: string[] }

const sha256 = (data: Buffer | string) => createHash('sha256').update(data).digest('hex');

test.describe('build manifest', () => {
  let manifest: Manifest;

  test.beforeAll(async ({ request }) => {
    const response = await request.get(`${SITE}/build-manifest.json`);
    expect(response.ok()).toBe(true);
    manifest = await response.json() as Manifest;
  });

  test('lists every file the site serves, by the bytes it serves', async ({ request }) => {
    const entries = Object.entries(manifest.files);
    expect(entries.length).toBeGreaterThan(10);
    expect(entries.map(([path]) => path)).toContain('index.csr.html');
    expect(entries.map(([path]) => path)).not.toContain('build-manifest.json');
    for (const [path, hash] of entries.filter(([path]) => /\.(js|css)$/.test(path))) {
      const body = await (await request.get(`${SITE}/${path}`)).body();
      expect(sha256(body), path).toBe(hash);
    }
  });

  for (const path of [...PUBLIC, ...VISITOR]) {
    test(`${path}: every script is the build's`, async ({ request }) => {
      const html = await (await request.get(SITE + path)).text();
      expect(checkPage(html, manifest, SITE)).toEqual([]);
    });
  }

  test('a page with anything else fails', () => {
    const page = (body: string) => `<html><head></head><body>${body}</body></html>`;
    expect(checkPage(page('<script>alert(1)</script>'), manifest, SITE)).toHaveLength(1);
    expect(checkPage(page('<script src="https://evil.example/x.js"></script>'), manifest, SITE)).toHaveLength(1);
    expect(checkPage(page('<script src="/not-built.js"></script>'), manifest, SITE)).toHaveLength(1);
    expect(checkPage(page('<img src="x" onerror="alert(1)">'), manifest, SITE)).toHaveLength(1);
    expect(checkPage(page('<a href="javascript:alert(1)">x</a>'), manifest, SITE)).toHaveLength(1);
    expect(checkPage(page('<script type="application/json" id="ng-state">{"a":1}</script>'), manifest, SITE)).toEqual([]);
  });

  test('the loader is recognised only as afterFirstPaint writes it', () => {
    const loader = 'let started = false; const start = () => { if (!started) { started = true; '
      + 'import(new URL("main-A.js", document.baseURI).href).then(() => import(new URL("chunk-B.js", document.baseURI).href)); } }; '
      + 'requestAnimationFrame(() => setTimeout(start)); setTimeout(start, 1000);';
    expect(loaderSources(loader)).toEqual(['main-A.js', 'chunk-B.js']);
    expect(loaderSources(loader + ' fetch("/steal")')).toBeNull();
    expect(loaderSources('alert(1)')).toBeNull();
  });
});
