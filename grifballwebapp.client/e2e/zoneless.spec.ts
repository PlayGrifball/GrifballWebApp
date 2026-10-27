import { expect, test, type Page, type Route, type WebSocketRoute } from '@playwright/test';

// The client runs without zone.js: nothing re-renders a page unless the component tells
// Angular to (signals, inputs, events). These tests go through the flows where that
// matters, with data that arrives after the first render: HTTP responses on navigation,
// a dialog that changes the page once it closes, a debounced search box and a SignalR push.
//
// The backend is mocked with page.route (and page.routeWebSocket for SignalR).

type Handler = (route: Route, url: URL) => Promise<void> | void;

const json = (route: Route, body: unknown) =>
  route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
const text = (route: Route, body: string) => route.fulfill({ status: 200, contentType: 'text/plain', body });

/** Logs in as a synthetic sysadmin + commissioner and routes /api calls to `handlers` (by path). */
async function mockBackend(page: Page, handlers: Record<string, Handler>): Promise<string[]> {
  const requests: string[] = [];

  await page.addInitScript(() => {
    localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'e2e-token', expiresIn: 3600, refreshToken: 'e2e-refresh' }));
    localStorage.setItem('metaInfo', JSON.stringify({ isSysAdmin: true, isCommissioner: true, isPlayer: false, displayName: 'E2E Admin', userID: 999 }));
  });

  // Keep the run hermetic (Google Fonts, jsDelivr).
  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));

  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    requests.push(`${route.request().method()} ${url.pathname}${url.search}`);
    if (url.pathname === '/api/identity/metaInfo') {
      return json(route, { isSysAdmin: true, isCommissioner: true, isPlayer: false, displayName: 'E2E Admin', userID: 999 });
    }
    if (url.pathname === '/api/CommitHash') return text(route, 'e2ehash');
    if (url.pathname === '/api/CommitDate') return text(route, '2026-01-02T03:04:05Z');
    if (url.pathname === '/api/Season/getCurrentSeasonID/') return json(route, 0);
    const handler = handlers[url.pathname];
    if (handler) return handler(route, url);
    return route.fulfill({ status: 404, body: '' });
  });

  return requests;
}

test('home renders the events and past seasons that load after navigation', async ({ page }) => {
  await mockBackend(page, {
    '/api/Home/CurrentAndFutureEvents/': route => json(route, [
      { seasonID: 4, name: 'Spring', start: '2026-03-01T00:00:00', end: '2026-05-01T00:00:00', eventType: 'Season' },
    ]),
    '/api/Home/PastSeasons': route => json(route, {
      totalCount: 1,
      results: [{ seasonID: 3, name: 'Winter', start: '2025-12-01T00:00:00', end: '2026-02-01T00:00:00', eventType: 'Season' }],
    }),
  });

  await page.goto('/');

  await expect(page.getByText('The Spring season will run from')).toBeVisible();
  await expect(page.locator('tr.mat-mdc-row')).toHaveText([/Winter/]);
  await expect(page.locator('mat-toolbar')).toContainText('e2ehash');
});

test('a season page reached by an in-app link renders the data it loads', async ({ page }) => {
  await mockBackend(page, {
    '/api/Home/CurrentAndFutureEvents/': route => json(route, []),
    '/api/Home/PastSeasons': route => json(route, {
      totalCount: 1,
      results: [{ seasonID: 3, name: 'Winter', start: '2025-12-01T00:00:00', end: '2026-02-01T00:00:00', eventType: 'Season' }],
    }),
    '/api/Season/getSeasonName/3': route => text(route, 'Winter Season'),
    '/api/TeamStandings/GetTeamStandings/3': route => json(route, [{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]),
    '/api/MatchPlanner/GetUnscheduledMatches/3': route => json(route, [
      { seasonMatchID: 21, homeCaptain: 'Home Cap', awayCaptain: 'Away Cap', complete: false, time: null },
    ]),
    '/api/MatchPlanner/GetScheduledMatches/3': route => json(route, []),
    '/api/Brackets/GetViewerData': route => json(route, { stages: [], matches: [], matchGames: [], participants: [] }),
  });

  await page.goto('/');
  await page.locator('tr.mat-mdc-row td', { hasText: 'Winter' }).click();

  await expect(page).toHaveURL(/\/season\/3$/);
  await expect(page.locator('h2')).toHaveText('Winter Season');
  await expect(page.locator('app-team-standings tr.mat-mdc-row')).toHaveText([/Alpha\s*4\s*1/]);
  await expect(page.locator('app-schedule-list')).toContainText('Home Cap vs Away Cap');
  await expect(page.locator('app-playoff-bracket')).toContainText('No bracket created');
});

test('processing a reschedule in the dialog refreshes the commissioner dashboard', async ({ page }) => {
  const reschedule = {
    matchRescheduleID: 8, seasonMatchID: 80, homeCaptain: 'Red Cap', awayCaptain: 'Blue Cap',
    originalScheduledTime: '2026-03-01T19:00:00Z', newScheduledTime: '2026-03-02T19:00:00Z',
    reason: 'Travel', requestedByGamertag: 'Red Cap', requestedAt: '2026-02-20T10:00:00Z', status: 0,
  };
  let processed = false;
  await mockBackend(page, {
    '/api/CommissionerDashboard/GetDashboardData': route => json(route, {
      pendingReschedules: processed ? [] : [reschedule],
      overdueMatches: [],
      summary: { pendingRescheduleCount: processed ? 0 : 1, overdueMatchCount: 0, criticalOverdueCount: 0 },
    }),
    '/api/Reschedule/ProcessReschedule/8': route => {
      processed = true;
      return json(route, {});
    },
  });

  await page.goto('/commissioner-dashboard');
  await expect(page.locator('mat-row')).toContainText(['Red Cap vs Blue Cap']);
  await expect(page.locator('.metric-value').first()).toHaveText('1');

  await page.getByRole('button', { name: 'Process' }).click();
  const dialog = page.locator('mat-dialog-container');
  await dialog.getByText('Approve Reschedule').click();
  await dialog.getByRole('button', { name: 'Approve' }).click();

  await expect(dialog).toBeHidden();
  await expect(page.getByText('No pending reschedule requests')).toBeVisible();
  await expect(page.locator('.metric-value').first()).toHaveText('0');
});

test('the user search box debounces typing and the table shows the filtered users', async ({ page }) => {
  const users = [
    { userID: 1, userName: 'grif', displayName: 'Grif', gamertag: 'SgtGrif' },
    { userID: 2, userName: 'simmons', displayName: 'Simmons', gamertag: 'Simmons' },
  ];
  const requests = await mockBackend(page, {
    '/api/UserManagement/GetUsers': (route, url) => {
      const search = url.searchParams.get('search') ?? '';
      const results = users.filter(u => u.userName.includes(search));
      return json(route, { totalCount: results.length, results });
    },
  });
  const searches = () => requests.filter(r => r.startsWith('GET /api/UserManagement/GetUsers') && r.includes('search='));

  await page.goto('/usermanagement');
  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(2);

  await page.getByLabel('Search').pressSequentially('grif', { delay: 50 });

  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(1);
  await expect(page.locator('tr.mat-mdc-row')).toContainText(['SgtGrif']);
  // Typed faster than the 300 ms debounce, so only the full term was searched for.
  expect(searches().map(r => new URL(r.split(' ')[1], 'http://x').searchParams.get('search'))).toEqual(['grif']);
});

test('the team builder renders a captain added by another user over SignalR', async ({ page }) => {
  const SEASON_ID = 7;
  await mockBackend(page, {
    [`/api/Teams/GetTeams/${SEASON_ID}`]: route => json(route, [
      { teamID: 1, teamName: 'Red Team', captain: { name: 'Captain Red', personID: 201, order: 1 }, players: [] },
    ]),
    [`/api/Teams/GetPlayerPool/${SEASON_ID}`]: route => json(route, [
      { name: 'Pool Alex', personID: 101, pick: null, round: null },
      { name: 'Pool Blair', personID: 102, pick: null, round: null },
    ]),
    [`/api/Teams/areCaptainsLocked/${SEASON_ID}`]: route => json(route, true),
  });

  // A minimal SignalR server: negotiate, then the JSON hub protocol over a WebSocket.
  const RS = '\u001e';
  await page.route('**/hub/TeamsHub/negotiate**', route => json(route, {
    negotiateVersion: 1, connectionId: 'e2e', connectionToken: 'e2e',
    availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }],
  }));
  const sockets: WebSocketRoute[] = [];
  await page.routeWebSocket(/\/hub\/TeamsHub/, ws => {
    // The page may restart the connection once the access token is read, so only the
    // newest open socket is used.
    ws.onClose(() => sockets.splice(sockets.indexOf(ws), 1));
    ws.onMessage(message => {
      // The first message is the handshake request; answer it, then the connection is open.
      if (String(message).includes('"protocol"')) {
        ws.send(`{}${RS}`);
        sockets.push(ws);
      }
    });
  });

  await page.goto(`/season/${SEASON_ID}/teams`);
  await expect(page.locator('mat-list-item.player-pool-item', { hasText: 'Pool Blair' })).toBeVisible();
  await expect.poll(() => sockets.length).toBeGreaterThan(0);

  const addCaptain = { seasonID: SEASON_ID, personID: 102, teamName: "Blair's Team", captainName: 'Pool Blair', orderNumber: 2 };
  sockets[sockets.length - 1].send(JSON.stringify({ type: 1, target: 'AddCaptain', arguments: [addCaptain] }) + RS);

  await expect(page.locator('mat-card.item:not(.dndPlaceholder) mat-card-title')).toHaveText(['1 - Red Team', "2 - Blair's Team"]);
  await expect(page.locator('mat-list-item.player-pool-item', { hasText: 'Pool Blair' })).toHaveCount(0);
});
