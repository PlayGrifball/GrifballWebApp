// Runs the production build as it is deployed: its own server (src/server.ts, built to
// dist/grifballwebapp.client/server/server.mjs) on E2E_PORT (default 4300), with the API it calls
// (when rendering pages, and for the browser's /api calls it passes on) answered from
// e2e/mockApi.mts, on the next port: as a visitor, or as the role the request's session cookie names
// (signIn in e2e/mockApi.mts).
//
// The server renders pages for "localhost" (one of the site's own host names) and sends the page
// that renders in the browser for any other, such as 127.0.0.1. So http://localhost:4300 is the
// site rendered on the server (Lighthouse, the layout test), and http://127.0.0.1:4300 renders in
// the browser, where the tests that mock the API with page.route control every answer.
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { fileURLToPath } from 'node:url';
import { mockResponse, roleOf } from './mockApi.mts';
import { handleTeamsHubUpgrade, isTeamsHubUpgrade, teamsHubNegotiateBody } from './teamsHubMock.mts';

const port = Number(process.env.E2E_PORT ?? 4300);
const apiPort = port + 1;
const serverEntry = fileURLToPath(new URL('../dist/grifballwebapp.client/server/server.mjs', import.meta.url));

// The backend, as the server reaches it: without the /api prefix. Plus the Teams SignalR hub
// (/hub/TeamsHub, proxied here as /TeamsHub): negotiate below, the WebSocket upgrade on 'upgrade'.
const backend = createServer((req, res) => {
  const bare = (req.url ?? '/').split('?')[0];
  if (bare === '/TeamsHub/negotiate') {
    const body = JSON.stringify(teamsHubNegotiateBody());
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(body);
    return;
  }
  const mocked = mockResponse(roleOf(req.headers.cookie), '/api' + bare);
  res.writeHead(mocked?.status ?? 404, { 'Content-Type': mocked?.contentType ?? 'text/plain' });
  res.end(mocked?.body ?? '');
});
backend.on('upgrade', (req, socket) => {
  if (isTeamsHubUpgrade(req))
    handleTeamsHubUpgrade(req, socket);
  else
    socket.destroy();
});
backend.listen(apiPort, '127.0.0.1', () => {
  // It prints "Serving on ..." once it listens (lighthouserc.cjs waits for that).
  const server = spawn(process.execPath, [serverEntry], {
    env: { ...process.env, PORT: String(port), API_URL: `http://127.0.0.1:${apiPort}` },
    stdio: 'inherit',
  });
  server.on('exit', code => process.exit(code ?? 1));
  for (const signal of ['SIGINT', 'SIGTERM'])
    process.on(signal, () => server.kill(signal));
});
