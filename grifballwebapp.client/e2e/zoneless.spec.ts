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

  // Keep the run hermetic (Google Fonts).
  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));

  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    requests.push(`${route.request().method()} ${url.pathname}${url.search}`);
    // Signed in (the session cookie the browser would have)
    if (url.pathname === '/api/Identity/Me') {
      return json(route, { isSysAdmin: true, isCommissioner: true, isPlayer: false, displayName: 'E2E Admin', userID: 999 });
    }
    if (url.pathname === '/api/Identity/Antiforgery') return json(route, { token: 'e2e-xsrf' });
    // As the API: every change from a cookie session carries the antiforgery token
    if (route.request().method() !== 'GET' && route.request().headers()['x-xsrf-token'] !== 'e2e-xsrf')
      return route.fulfill({ status: 400, headers: { 'X-Antiforgery-Rejected': 'true' }, body: '' });
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
      { seasonID: 4, name: 'Spring', start: '2026-03-01T00:00:00Z', end: '2026-05-01T00:00:00Z', eventType: 'Season' },
    ]),
    '/api/Home/PastSeasons': route => json(route, {
      totalCount: 1,
      results: [{ seasonID: 3, name: 'Winter', start: '2025-12-01T00:00:00Z', end: '2026-02-01T00:00:00Z', eventType: 'Season' }],
    }),
  });

  await page.goto('/');

  await expect(page.getByText('The Spring season will run from')).toBeVisible();
  await expect(page.locator('tr.mat-mdc-row')).toHaveText([/Winter/]);
  await expect(page.locator('app-site-footer')).toContainText('e2ehash');
});

test('a season page reached by an in-app link renders the data it loads', async ({ page }) => {
  await mockBackend(page, {
    '/api/Home/CurrentAndFutureEvents/': route => json(route, []),
    '/api/Home/PastSeasons': route => json(route, {
      totalCount: 1,
      results: [{ seasonID: 3, name: 'Winter', start: '2025-12-01T00:00:00Z', end: '2026-02-01T00:00:00Z', eventType: 'Season' }],
    }),
    '/api/Season/getSeasonName/3': route => text(route, 'Winter Season'),
    '/api/TeamStandings/GetTeamStandings/3': route => json(route, [{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]),
    '/api/MatchPlanner/GetUnscheduledMatches/3': route => json(route, [
      { seasonMatchID: 21, homeCaptain: 'Home Cap', awayCaptain: 'Away Cap', complete: false },
    ]),
    '/api/MatchPlanner/GetScheduledMatches/3': route => json(route, []),
    '/api/Brackets/GetViewerData': route => json(route, { stages: [], matches: [], matchGames: [], participants: [] }),
  });

  await page.goto('/');
  await page.locator('tr.mat-mdc-row').getByRole('link', { name: 'Winter' }).click();

  await expect(page).toHaveURL(/\/season\/3$/);
  await expect(page.locator('h1')).toHaveText('Winter Season');
  await expect(page.locator('app-team-standings tr.mat-mdc-row')).toHaveText([/Alpha\s*4\s*1/]);
  // The schedule and the bracket are tabs of the season page.
  await page.getByRole('link', { name: 'Schedule', exact: true }).click();
  await expect(page).toHaveURL(/\/season\/3\?tab=schedule$/);
  await expect(page.locator('app-schedule-list')).toContainText('Home Cap vs Away Cap');
  await page.getByRole('link', { name: 'Bracket', exact: true }).click();
  await expect(page.locator('app-playoff-bracket')).toContainText('No bracket created');
});

test('a season with a bracket draws it with the bundled viewer and links its matches', async ({ page }) => {
  const team = (id: number, name: string) => ({ id, tournament_id: 3, name });
  const side = (id: number, position: number, extra: object = {}) => ({ id, position, ...extra });
  // Four teams, single elimination: one semifinal played, the other ready, the final waiting.
  const bracket = {
    stages: [{
      id: 1, tournament_id: 3, name: 'Winter Playoffs', type: 'single_elimination', number: 1,
      settings: { size: 4, seedOrdering: ['natural'], grandFinal: 'none', consolationFinal: false },
    }],
    matches: [
      { id: 101, stage_id: 1, group_id: 1, round_id: 1, number: 1, child_count: 0, status: 4,
        opponent1: side(1, 1, { score: 3, result: 'win' }), opponent2: side(2, 2, { score: 1, result: 'loss' }) },
      { id: 102, stage_id: 1, group_id: 1, round_id: 1, number: 2, child_count: 0, status: 2,
        opponent1: side(3, 3), opponent2: side(4, 4) },
      { id: 103, stage_id: 1, group_id: 1, round_id: 2, number: 1, child_count: 0, status: 1,
        opponent1: { id: 1 }, opponent2: { id: null } },
    ],
    matchGames: [],
    participants: [team(1, 'Alpha'), team(2, 'Bravo'), team(3, 'Charlie'), team(4, 'Delta')],
  };

  await mockBackend(page, {
    '/api/Season/getSeasonName/3': route => text(route, 'Winter Season'),
    '/api/TeamStandings/GetTeamStandings/3': route => json(route, []),
    '/api/MatchPlanner/GetUnscheduledMatches/3': route => json(route, []),
    '/api/MatchPlanner/GetScheduledMatches/3': route => json(route, []),
    '/api/Brackets/GetViewerData': route => json(route, bracket),
  });

  await page.goto('/season/3?tab=bracket');

  const viewer = page.locator('app-playoff-bracket .brackets-viewer');
  await expect(viewer.locator('.match')).toHaveCount(3);
  await expect(viewer.locator('.round')).toHaveCount(2);
  for (const name of ['Alpha', 'Bravo', 'Charlie', 'Delta']) {
    await expect(viewer.locator('.participant .name', { hasText: name }).first()).toBeVisible();
  }
  await expect(viewer.locator('.participant.win')).toContainText('Alpha');
  await expect(page.locator('app-playoff-bracket')).not.toContainText('No bracket created');
  // The viewer's own stylesheet is bundled too: matches are laid out by it, not stacked as plain divs.
  await expect(viewer.locator('.round').first()).toHaveCSS('display', 'flex');

  await viewer.locator('.match', { hasText: 'Charlie' }).click();
  await expect(page).toHaveURL(/\/seasonmatch\/102$/);
});

test('a double-elimination bracket draws its losers bracket and grand final', async ({ page }) => {
  const team = (id: number, name: string) => ({ id, tournament_id: 3, name });
  const match = (id: number, group_id: number, round_id: number, number: number, status: number, opponent1: object | null, opponent2: object | null) =>
    ({ id, stage_id: 3, group_id, round_id, number, child_count: 0, status, opponent1, opponent2 });
  // Shaped like BracketService.GetViewerData: winners in group 1, losers in group 2 with round ids
  // continuing after the last winners round, grand final and its reset in group 3 at round 0.
  const bracket = {
    stages: [{
      id: 3, tournament_id: 3, name: 'Winter Playoffs', type: 'double_elimination', number: 1,
      settings: { size: 4, seedOrdering: ['inner_outer', 'natural', 'reverse_half_shift', 'reverse'], grandFinal: 'double', consolationFinal: false },
    }],
    matches: [
      match(201, 1, 1, 1, 4, { id: 1, position: 1, score: 3, result: 'win' }, { id: 4, position: 4, score: 1, result: 'loss' }),
      match(202, 1, 1, 2, 4, { id: 2, position: 2, score: 2, result: 'loss' }, { id: 3, position: 3, score: 3, result: 'win' }),
      match(203, 1, 2, 1, 2, { id: 1 }, { id: 3 }),
      match(204, 2, 3, 1, 4, { id: 4, position: 1, score: 3, result: 'win' }, { id: 2, position: 2, score: 0, result: 'loss' }),
      match(205, 2, 4, 1, 1, { id: 4 }, { id: null, position: 1 }),
      match(206, 3, 0, 1, 0, { id: null }, { id: null, position: 1 }),
      match(207, 3, 0, 2, 0, { id: null }, { id: null }),
    ],
    matchGames: [],
    participants: [team(1, 'Alpha'), team(2, 'Bravo'), team(3, 'Charlie'), team(4, 'Delta')],
  };

  await mockBackend(page, {
    '/api/Season/getSeasonName/3': route => text(route, 'Winter Season'),
    '/api/TeamStandings/GetTeamStandings/3': route => json(route, []),
    '/api/MatchPlanner/GetUnscheduledMatches/3': route => json(route, []),
    '/api/MatchPlanner/GetScheduledMatches/3': route => json(route, []),
    '/api/Brackets/GetViewerData': route => json(route, bracket),
  });

  await page.goto('/season/3?tab=bracket');

  const viewer = page.locator('app-playoff-bracket .brackets-viewer');
  // The component's locale names the winners and losers brackets after the stage.
  const [winners, losers] = [viewer.locator('.bracket').nth(0), viewer.locator('.bracket').nth(1)];
  await expect(viewer.locator('.bracket h2')).toHaveText(['Winter Playoffs', 'Winter Playoffs - Losers']);
  await expect(losers.locator('.round h3')).toHaveText(['LB Round 1', 'LB Final Round']);
  // The viewer draws the grand final as the last rounds of the winners bracket.
  await expect(winners.locator('.round h3').nth(2)).toHaveText('Grand Final');
  await expect(winners.locator('.round').nth(2).locator('.match[data-match-id="206"]')).toHaveCount(1);
  await expect(winners.locator('.match[data-match-id="207"]')).toHaveCount(1);
  await expect(winners.locator('.match')).toHaveCount(5);
  await expect(losers.locator('.match')).toHaveCount(2);

  const losersFirst = losers.locator('.match').first();
  await expect(losersFirst.locator('.participant.win')).toContainText('Delta');
  await expect(losersFirst.locator('.participant.loss')).toContainText('Bravo');

  await losersFirst.click();
  await expect(page).toHaveURL(/\/seasonmatch\/204$/);
});

test('processing a reschedule in the dialog refreshes the commissioner dashboard', async ({ page }) => {
  const reschedule = {
    matchRescheduleID: 8, seasonMatchID: 80, homeCaptain: 'Red Cap', awayCaptain: 'Blue Cap',
    originalScheduledTime: '2026-03-01T19:00:00Z', newScheduledTime: '2026-03-02T19:00:00Z',
    reason: 'Travel', requestedByGamertag: 'Red Cap', requestedAt: '2026-02-20T10:00:00Z', status: 'Pending',
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
  // GrifballWebApp.Server UserResponseDto, as the API sends it.
  const user = (userID: number, userName: string, displayName: string, gamertag: string) => ({
    userID, userName, lockoutEnd: null, lockoutEnabled: true, isDummyUser: false, accessFailedCount: 0, region: null,
    displayName, gamertag, discord: null, externalAuthCount: 0, hasPassword: true, roles: [],
  });
  const users = [user(1, 'grif', 'Grif', 'SgtGrif'), user(2, 'simmons', 'Simmons', 'Simmons')];
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

  // 'g' alone already narrows the table to one row, so wait for the request for the full
  // term before looking at the table.
  const terms = () => searches().map(r => new URL(r.split(' ')[1], 'http://x').searchParams.get('search'));
  await expect.poll(terms).toContain('grif');
  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(1);
  await expect(page.locator('tr.mat-mdc-row')).toContainText(['SgtGrif']);
  // The debounce kept it below one request per keystroke (exact counts depend on timing).
  expect(terms().at(-1)).toBe('grif');
  expect(terms().length).toBeLessThan('grif'.length);
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

test('the signup form ticks single slots and whole days, and submits the chosen option ids', async ({ page }) => {
  // Exactly what GET /api/Signups/getTimeslots sends (GrifballWebApp.Server.Signups.TimeslotDto):
  // optionID, the day as the enum name, the time as "HH:mm:ss", isChecked.
  const timeslots = [
    { optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
    { optionID: 42, dayOfWeek: 'Monday', time: '20:00:00', isChecked: true },
    { optionID: 57, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false },
    { optionID: 58, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false },
  ];
  const posted: any[] = [];
  await mockBackend(page, {
    // No signup yet: the action returns Ok(null), which ASP.NET sends as 204 No Content.
    '/api/Signups/getSignup/5': route => route.fulfill({ status: 204, body: '' }),
    '/api/Signups/getTimeslots/5': route => json(route, timeslots),
    '/api/Signups/UpsertSignup/': route => {
      posted.push(route.request().postDataJSON());
      return route.fulfill({ status: 200, body: '' });
    },
  });

  await page.goto('/season/5/signupForm');
  const rows = page.locator('app-availability-table tr.mat-mdc-row');
  await expect(rows).toHaveCount(2);
  const boxes = (row: number) => rows.nth(row).locator('mat-checkbox input');
  const checked = async (row: number) => Promise.all((await boxes(row).all()).map(b => b.isChecked()));
  expect(await checked(0)).toEqual([false, true]);
  expect(await checked(1)).toEqual([false, false]);

  // One box ticks only that slot.
  await boxes(0).nth(0).check();
  await expect.poll(() => checked(0)).toEqual([true, true]);
  expect(await checked(1)).toEqual([false, false]);

  // The day header ticks only its own row.
  await rows.nth(1).locator('td button', { hasText: 'Tuesday' }).click();
  await expect.poll(() => checked(1)).toEqual([true, true]);
  expect(await checked(0)).toEqual([true, true]);

  // Clicking it again clears that row only.
  await rows.nth(1).locator('td button', { hasText: 'Tuesday' }).click();
  await expect.poll(() => checked(1)).toEqual([false, false]);

  await boxes(1).nth(1).check(); // Tuesday 20:00
  await page.getByRole('button', { name: 'Submit' }).click();

  await expect.poll(() => posted.length).toBe(1);
  expect(posted[0].timeslots).toEqual([
    { optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: true },
    { optionID: 42, dayOfWeek: 'Monday', time: '20:00:00', isChecked: true },
    { optionID: 57, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false },
    { optionID: 58, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: true },
  ]);
  expect(posted[0].seasonID).toBe(5);
});
