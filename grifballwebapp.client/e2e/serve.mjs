// Minimal static server for the production build, mirroring nginx (see nginx.conf and
// default.conf): the `try_files $uri $uri/ /index.html` SPA fallback, gzip, and long caching of
// the hashed files only, so Lighthouse measures the app as it is served.
// API and SignalR traffic is mocked in the tests with page.route, so this
// server never proxies anything. With MOCK_API=1 it answers /api itself from
// e2e/mockApi.mts, as a visitor who is not logged in (for Lighthouse CI).
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, normalize, resolve } from 'node:path';
import { gzipSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../dist/grifballwebapp.client/browser', import.meta.url)));
const port = Number(process.env.E2E_PORT ?? 4300);
const mockApi = process.env.MOCK_API ? await import('./mockApi.mts') : null;
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.ico': 'image/x-icon', '.png': 'image/png', '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml', '.webp': 'image/webp', '.woff2': 'font/woff2', '.woff': 'font/woff', '.txt': 'text/plain',
};

async function fileFor(urlPath) {
  const candidate = normalize(join(root, decodeURIComponent(urlPath)));
  if (!candidate.startsWith(root)) return null;
  try {
    const s = await stat(candidate);
    if (s.isFile()) return candidate;
    if (s.isDirectory()) {
      const index = join(candidate, 'index.html');
      if ((await stat(index)).isFile()) return index;
    }
  } catch { /* fall through to the SPA fallback */ }
  return null;
}

// As default.conf: main-<hash>.js, chunk-<hash>.js, styles-<hash>.css and media/ are kept for good.
const hashed = /^\/([a-z]+-[A-Za-z0-9_-]{8}\.(js|css)|media\/.+)$/;
const compressible = new Set(['.html', '.js', '.css', '.json', '.svg', '.txt']);
const gzipped = new Map();

createServer(async (req, res) => {
  const path = (req.url ?? '/').split('?')[0];
  if (mockApi && (path.startsWith('/api/') || path.startsWith('/hub/'))) {
    const mocked = mockApi.mockResponse('anon', path);
    res.writeHead(mocked?.status ?? 404, { 'Content-Type': mocked?.contentType ?? 'text/plain' });
    res.end(mocked?.body ?? '');
    return;
  }
  const file = (await fileFor(path)) ?? join(root, 'index.html');
  try {
    let body = await readFile(file);
    const headers = {
      'Content-Type': types[extname(file)] ?? 'application/octet-stream',
      'Cache-Control': hashed.test(path) ? 'public, max-age=31536000, immutable' : 'no-cache',
      'Vary': 'Accept-Encoding',
    };
    if (compressible.has(extname(file)) && body.length >= 1024 && /\bgzip\b/.test(req.headers['accept-encoding'] ?? '')) {
      if (!gzipped.has(file)) gzipped.set(file, gzipSync(body, { level: 6 }));
      body = gzipped.get(file);
      headers['Content-Encoding'] = 'gzip';
    }
    res.writeHead(200, headers);
    res.end(body);
  } catch (e) {
    res.writeHead(500);
    res.end(String(e));
  }
}).listen(port, '127.0.0.1', () => console.log(`Serving ${root} on http://127.0.0.1:${port}`));
