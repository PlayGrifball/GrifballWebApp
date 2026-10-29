// The client's web server (the image's entry point, in place of nginx). It serves the built files,
// renders the public pages on the server (src/app/app.routes.server.ts), and passes /api, /hub and
// /signin-discord on to the backend, as nginx.conf and default.conf did.
//
// Environment:
//   PORT              where to listen (default 80, as nginx did)
//   API_URL           the backend (default http://grifballwebapp-server:5295)
//   NG_ALLOWED_HOSTS  host names to render pages for, comma separated, on top of the site's own
//                     (grifball.xyz and its subdomains) and localhost. "*.example.com" allows the
//                     subdomains. A request for any other host, or an address (ex. a probe on the
//                     pod's), gets the page that renders in the browser instead.
import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import compression from 'compression';
import express, { type Request, type Response } from 'express';
import { createProxyMiddleware } from 'http-proxy-middleware';
import type { ClientRequest, IncomingMessage } from 'node:http';
import { join } from 'node:path';
import { pageCacheHeaders } from './app/pageCache';

const browserDistFolder = join(import.meta.dirname, '../browser');
const apiUrl = (process.env['API_URL'] ?? 'http://grifballwebapp-server:5295').replace(/\/+$/, '');
const allowedHosts = [
  'grifball.xyz',
  '*.grifball.xyz',
  'localhost',
  ...(process.env['NG_ALLOWED_HOSTS'] ?? '').split(',').map(host => host.trim()).filter(host => host !== ''),
];

/** Whether pages for this Host header may be rendered (the same rule Angular's engine applies). */
export function isAllowedHost(host: string | undefined, allowed: readonly string[] = allowedHosts): boolean {
  const hostname = (host ?? '').replace(/:\d+$/, '').replace(/^\[(.*)\]$/, '$1').toLowerCase();
  return hostname !== '' && allowed.some(entry => entry === hostname || (entry.startsWith('*.') && hostname.endsWith(entry.slice(1))));
}

const app = express();
const angularApp = new AngularNodeAppEngine({ allowedHosts });

// Not "X-Powered-By: Express"
app.disable('x-powered-by');

// The backend, as nginx proxied it: /api/x and /hub/x to /x, /signin-discord as it is (the Discord
// sign-in callback), with the same headers. The request's own headers go along (the backend reads
// the client from X-Real-IP, set by the ingress), with its Host, and nginx's X-Forwarded-For
// (with this hop added), X-Forwarded-Proto (the scheme it was reached on) and X-Forwarded-Host.
const toBackend = (prefix: string) => createProxyMiddleware<Request, Response>({
  target: apiUrl,
  changeOrigin: false,
  pathFilter: path => path === prefix || path.startsWith(prefix + '/'),
  pathRewrite: path => prefix === '/signin-discord' ? path : path.slice(prefix.length) || '/',
  // SignalR's WebSocket
  ws: prefix === '/hub',
  proxyTimeout: 100_000,
  on: {
    proxyReq: (proxyReq, req) => setForwardedHeaders(proxyReq, req),
    proxyReqWs: (proxyReq, req) => setForwardedHeaders(proxyReq, req),
  },
});

/** The X-Forwarded-* headers nginx set: $proxy_add_x_forwarded_for, $scheme and $http_host. */
export function setForwardedHeaders(proxyReq: Pick<ClientRequest, 'setHeader'>, req: IncomingMessage): void {
  const forwardedFor = req.headers['x-forwarded-for'];
  const peer = req.socket.remoteAddress ?? '';
  proxyReq.setHeader('X-Forwarded-For', forwardedFor ? `${forwardedFor}, ${peer}` : peer);
  proxyReq.setHeader('X-Forwarded-Proto', 'encrypted' in req.socket && req.socket.encrypted ? 'https' : 'http');
  if (req.headers.host)
    proxyReq.setHeader('X-Forwarded-Host', req.headers.host);
}
const proxies = [toBackend('/api'), toBackend('/hub'), toBackend('/signin-discord')];
for (const proxy of proxies)
  app.use(proxy);

// Text responses compressed, as nginx did (gzip_min_length 1024). Not the SignalR stream (passed
// through above) or anything already compressed.
app.use(compression({ threshold: 1024 }));

// The build's files. main-<hash>.js, chunk-<hash>.js, styles-<hash>.css and media/ get new names
// with each build, so browsers keep them for good; anything else is checked on every visit.
const hashed = /^\/([a-z]+-[A-Za-z0-9_-]{8}\.(js|css)|media\/.+)$/;
app.use(
  express.static(browserDistFolder, {
    index: false,
    redirect: false,
    setHeaders: (res, path) => {
      const url = '/' + path.slice(browserDistFolder.length + 1).replaceAll('\\', '/');
      res.setHeader('Cache-Control', hashed.test(url) ? 'public, max-age=31536000, immutable' : 'no-cache');
    },
  }),
);

/** The page that loads the app and renders in the browser (as nginx served index.html). */
function sendClientPage(res: Response, next: (err?: unknown) => void): void {
  res.setHeader('Cache-Control', 'no-cache');
  // A client that went away before the end (ECONNABORTED) is no error of ours
  res.sendFile(join(browserDistFolder, 'index.csr.html'), err => {
    if (err && (err as NodeJS.ErrnoException).code !== 'ECONNABORTED')
      next(err);
  });
}

// For hosts this server does not render for
app.use((req, res, next) => {
  if (isAllowedHost(req.headers.host))
    return next();
  sendClientPage(res, next);
});

// Every other request: the page, rendered on the server or (for the pages in
// app.routes.server.ts that render in the browser) the page that loads the app. Should rendering
// fail, the browser renders the page instead. A page rendered with the visitor's session cookie
// (which its API calls carried) is theirs alone: private and no-store (app/pageCache.ts).
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then(response => {
      if (!response)
        return sendClientPage(res, next);
      for (const [name, value] of Object.entries(pageCacheHeaders(req.headers.cookie)))
        response.headers.set(name, value);
      return writeResponseToNodeResponse(response, res);
    })
    .catch(error => {
      console.error('Rendering ' + req.originalUrl + ' failed, sending the page that renders in the browser:', error);
      sendClientPage(res, next);
    });
});

if (isMainModule(import.meta.url)) {
  const port = Number(process.env['PORT'] ?? 80);
  const server = app.listen(port, error => {
    if (error)
      throw error;
    console.log(`Serving on http://localhost:${port}, the API at ${apiUrl}`);
  });
  // SignalR's WebSocket upgrade, for /hub
  server.on('upgrade', proxies[1].upgrade);
}

/** The request handler for the Angular CLI (ng serve, and the build's route extraction). */
export const reqHandler = createNodeRequestHandler(app);
