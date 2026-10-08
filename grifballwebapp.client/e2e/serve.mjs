// Minimal static server for the production build, mirroring nginx's
// `try_files $uri $uri/ /index.html` SPA fallback (see default.conf).
// API and SignalR traffic is mocked in the tests with page.route, so this
// server never proxies anything.
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../dist/grifballwebapp.client/browser', import.meta.url)));
const port = Number(process.env.E2E_PORT ?? 4300);
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.ico': 'image/x-icon', '.png': 'image/png', '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml', '.webp': 'image/webp', '.woff2': 'font/woff2', '.txt': 'text/plain',
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

createServer(async (req, res) => {
  const path = (req.url ?? '/').split('?')[0];
  const file = (await fileFor(path)) ?? join(root, 'index.html');
  try {
    const body = await readFile(file);
    res.writeHead(200, { 'Content-Type': types[extname(file)] ?? 'application/octet-stream' });
    res.end(body);
  } catch (e) {
    res.writeHead(500);
    res.end(String(e));
  }
}).listen(port, '127.0.0.1', () => console.log(`Serving ${root} on http://127.0.0.1:${port}`));
