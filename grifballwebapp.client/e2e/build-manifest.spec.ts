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

  // What a dishonest server might add to a page, each written the ways a hand-written check would miss
  const TAMPERED: Record<string, string> = {
    'an inline script': '<script>alert(1)</script>',
    'an inline script, end tag spaced': '<script >alert(1)</script >',
    'an inline script in a template': '<template><script>alert(1)</script></template>',
    'an inline script with data-src': '<script data-src="index.csr.html">alert(1)</script>',
    'an inline script with data-type json': '<script data-type="application/json">alert(1)</script>',
    'a script from another site': '<script src="https://evil.example/x.js"></script>',
    'a script from another site, unquoted': '<script src=https://evil.example/x.js></script>',
    'an SVG script from another site': '<svg><script href="https://evil.example/x.js"></script></svg>',
    'a script not in the build': '<script src="/not-built.js"></script>',
    'a build file with a query string': '<script src="/index.csr.html?v=2"></script>',
    'a base element': '<base href="https://evil.example/">',
    'an inline handler': '<img src="x" onerror="alert(1)">',
    'an inline handler, single quoted': `<img src=x onerror='alert(1)'>`,
    'an inline handler, unquoted': '<img src=x onerror=alert(1)>',
    'a javascript: URL': '<a href="javascript:alert(1)">x</a>',
    'a javascript: URL, unquoted': '<a href=javascript:alert(1)>x</a>',
    'a javascript: URL with a tab': '<a href="java\tscript:alert(1)">x</a>',
    'an iframe with srcdoc': '<iframe srcdoc="&lt;script&gt;alert(1)&lt;/script&gt;"></iframe>',
    'an object': '<object data="https://evil.example/x"></object>',
  };
  const page = (body: string) => `<!doctype html><html><head></head><body>${body}</body></html>`;

  for (const [what, body] of Object.entries(TAMPERED)) {
    test(`a page with ${what} fails`, () => {
      expect(checkPage(page(body), manifest, SITE)).not.toEqual([]);
    });
  }

  test('JSON data and the stylesheet swap pass', () => {
    const body = '<script type="application/json" id="ng-state">{"a":1}</script>'
      + `<link rel="stylesheet" href="${Object.keys(manifest.files).find(path => path.endsWith('.css'))}" media="print" onload="this.media='all'">`;
    expect(checkPage(page(body), manifest, SITE)).toEqual([]);
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
