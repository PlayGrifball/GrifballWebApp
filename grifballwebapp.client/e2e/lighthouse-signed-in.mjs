// Checks, before Lighthouse audits the pages signed in, that the server does render each of them
// for the signed-in user: asked as Lighthouse asks (lighthouserc.cjs with LIGHTHOUSE_SESSION=signed-in,
// its URLs and the Cookie header it sends), each page must have that user's name in the HTML the
// server drew (not only in the API answers it passes on to the browser) and be kept out of caches
// as a signed-in user's page is (src/app/pageCache.ts). Otherwise the signed-in scores would be a
// visitor's.
// Usage: node e2e/lighthouse-signed-in.mjs (after the build; starts and stops e2e/serve.mjs itself)
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import { META } from './mockApi.mts';

process.env.LIGHTHOUSE_SESSION = 'signed-in';
const { notFoundPage, browserPages, ci: { collect } } = createRequire(import.meta.url)('../lighthouserc.cjs');
const name = META.admin.displayName;

const server = spawn(process.execPath, [new URL('serve.mjs', import.meta.url).pathname], { stdio: ['ignore', 'pipe', 'inherit'] });
await new Promise((resolve, reject) => {
  server.stdout.on('data', chunk => { if (String(chunk).includes('Serving')) resolve(); });
  server.on('exit', code => reject(new Error(`e2e/serve.mjs exited (${code})`)));
});

let failed = 0;
try {
  for (const url of collect.url) {
    const response = await fetch(url, { headers: collect.settings.extraHeaders });
    const page = new URL(url).pathname + new URL(url).search;
    // The page for an address the site has no page for is drawn for them too, answered with a 404
    const status = page === notFoundPage ? 404 : 200;
    // The page as drawn, without its scripts (the transfer cache has the API's answers, the name too)
    const html = (await response.text()).replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, '');
    const problems = [
      response.status !== status && `answered ${response.status}`,
      // (Not the pages the browser draws: the server sends anyone the same)
      !browserPages.includes(page) && !html.includes(name) && `does not show "${name}"`,
      response.headers.get('cache-control') !== 'private, no-store' && `is cached as a visitor's page (${response.headers.get('cache-control')})`,
    ].filter(Boolean);
    if (problems.length) {
      failed++;
      console.log(`::error title=Lighthouse signed in::${page} was not rendered for the signed-in user: it ${problems.join(', ')}`);
    } else {
      console.log(`${page}: rendered for ${name}`);
    }
  }
} finally {
  server.kill('SIGTERM');
}
process.exit(failed ? 1 : 0);
