import { expect, test, type Page, type Route, type WebSocketRoute } from '@playwright/test';
import { RS, negotiateResponse } from './hubMock.mts';

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
    if (url.pathname === '/api/identity/me') {
      return json(route, { isSysAdmin: true, isCommissioner: true, isPlayer: false, displayName: 'E2E Admin', userID: 999 });
    }
    if (url.pathname === '/api/identity/antiforgery-token') return json(route, { token: 'e2e-xsrf' });
    // As the API: every change from a cookie session carries the antiforgery token
    if (route.request().method() !== 'GET' && route.request().headers()['x-xsrf-token'] !== 'e2e-xsrf')
      return route.fulfill({ status: 400, headers: { 'X-Antiforgery-Rejected': 'true' }, body: '' });
    if (url.pathname === '/api/CommitHash') return text(route, 'e2ehash');
    if (url.pathname === '/api/CommitDate') return text(route, '2026-01-02T03:04:05Z');
    if (url.pathname === '/api/seasons/current') return json(route, 0);
    const handler = handlers[url.pathname];
    if (handler) return handler(route, url);
    return route.fulfill({ status: 404, body: '' });
  });

  return requests;
}

test('home renders the events and past seasons that load after navigation', async ({ page }) => {
  await mockBackend(page, {
    '/api/events': route => json(route, [
      { seasonID: 4, name: 'Spring', start: '2026-03-01T00:00:00Z', end: '2026-05-01T00:00:00Z', eventType: 'Season' },
    ]),
    '/api/seasons': route => json(route, {
      totalCount: 1,
      results: [{ seasonID: 3, seasonName: 'Winter', start: '2025-12-01T00:00:00Z', end: '2026-02-01T00:00:00Z', eventType: 'Season' }],
    }),
  });

  await page.goto('/');

  await expect(page.getByText('The Spring season will run from')).toBeVisible();
  await expect(page.locator('tr.mat-mdc-row')).toHaveText([/Winter/]);
  await expect(page.locator('app-site-footer')).toContainText('e2ehash');
});

test('a season page reached by an in-app link renders the data it loads', async ({ page }) => {
  await mockBackend(page, {
    '/api/events': route => json(route, []),
    '/api/seasons': route => json(route, {
      totalCount: 1,
      results: [{ seasonID: 3, seasonName: 'Winter', start: '2025-12-01T00:00:00Z', end: '2026-02-01T00:00:00Z', eventType: 'Season' }],
    }),
    '/api/seasons/3/summary': route => json(route, { seasonID: 3, seasonName: 'Winter Season', year: 2025, leagueID: 1, leagueName: 'AGLA', isHistorical: false, hasEnded: false, teamCount: 1, signupCount: 0, matchCount: 1, bracketMatchCount: 0, playerStatCount: 0, champions: [] }),
    '/api/seasons/3/standings': route => json(route, [{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]),
    '/api/seasons/3/matches/unscheduled': route => json(route, [
      { seasonMatchID: 21, homeCaptain: 'Home Cap', awayCaptain: 'Away Cap', homeTeam: 'Home Cap', awayTeam: 'Away Cap', complete: false },
    ]),
    '/api/seasons/3/matches/scheduled': route => json(route, []),
    '/api/seasons/3/bracket/viewer-data': route => json(route, { stages: [], matches: [], matchGames: [], participants: [] }),
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
    '/api/seasons/3/summary': route => json(route, { seasonID: 3, seasonName: 'Winter Season', year: 2025, leagueID: 1, leagueName: 'AGLA', isHistorical: false, hasEnded: false, teamCount: 1, signupCount: 0, matchCount: 1, bracketMatchCount: 0, playerStatCount: 0, champions: [] }),
    '/api/seasons/3/standings': route => json(route, []),
    '/api/seasons/3/matches/unscheduled': route => json(route, []),
    '/api/seasons/3/matches/scheduled': route => json(route, []),
    '/api/seasons/3/bracket/viewer-data': route => json(route, bracket),
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
    '/api/seasons/3/summary': route => json(route, { seasonID: 3, seasonName: 'Winter Season', year: 2025, leagueID: 1, leagueName: 'AGLA', isHistorical: false, hasEnded: false, teamCount: 1, signupCount: 0, matchCount: 1, bracketMatchCount: 0, playerStatCount: 0, champions: [] }),
    '/api/seasons/3/standings': route => json(route, []),
    '/api/seasons/3/matches/unscheduled': route => json(route, []),
    '/api/seasons/3/matches/scheduled': route => json(route, []),
    '/api/seasons/3/bracket/viewer-data': route => json(route, bracket),
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
    canApprove: true, whyNotApprovable: null,
  };
  let processed = false;
  await mockBackend(page, {
    '/api/commissioner-dashboard': route => json(route, {
      pendingReschedules: processed ? [] : [reschedule],
      overdueMatches: [],
      summary: { pendingRescheduleCount: processed ? 0 : 1, overdueMatchCount: 0, criticalOverdueCount: 0 },
    }),
    '/api/reschedules/8/decision': route => {
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
    '/api/users': (route, url) => {
      const search = url.searchParams.get('search') ?? '';
      const results = users.filter(u => u.userName.includes(search));
      return json(route, { totalCount: results.length, results });
    },
  });
  const searches = () => requests.filter(r => r.startsWith('GET /api/users') && r.includes('search='));

  await page.goto('/usermanagement');
  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(2);

  await page.getByRole('textbox', { name: 'Search users' }).pressSequentially('grif', { delay: 50 });

  // 'g' alone already narrows the table to one row, so wait for the request for the full
  // term before looking at the table.
  const terms = () => searches().map(r => new URL(r.split(' ')[1], 'https://x').searchParams.get('search'));
  await expect.poll(terms).toContain('grif');
  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(1);
  await expect(page.locator('tr.mat-mdc-row')).toContainText(['SgtGrif']);
  // The debounce kept it below one request per keystroke (exact counts depend on timing).
  expect(terms().at(-1)).toBe('grif');
  expect(terms().length).toBeLessThan('grif'.length);
});

test('the team builder renders a captain added by another user over SignalR', async ({ page }) => {
  const SEASON_ID = 7;
  // The server's draft, which the other user's change updates too: the page reloads it whenever its
  // hub connection comes up, which may be just after the update arrives.
  let captainAdded = false;
  await mockBackend(page, {
    [`/api/seasons/${SEASON_ID}/draft/teams`]: route => json(route, [
      { teamID: 1, teamName: 'Red Team', captain: { name: 'Captain Red', personID: 201, order: 1 }, players: [] },
      ...(captainAdded ? [{ teamID: 2, teamName: "Blair's Team", captain: { name: 'Pool Blair', personID: 102, order: 2 }, players: [] }] : []),
    ]),
    [`/api/seasons/${SEASON_ID}/draft/player-pool`]: route => json(route, [
      { name: 'Pool Alex', personID: 101, pick: null, round: null },
      ...(captainAdded ? [] : [{ name: 'Pool Blair', personID: 102, pick: null, round: null }]),
    ]),
    [`/api/seasons/${SEASON_ID}/draft/captains/locked`]: route => json(route, true),
  });

  // A minimal SignalR server: a negotiate offering WebSockets, then the JSON hub protocol over one. RS and
  // negotiateResponse are shared with e2e/hubMock.mts, which answers the same hub for Lighthouse and the
  // layout test (over a real socket there, so it can't share the rest of this route-based code).
  let negotiated = false;
  await page.route('**/hub/TeamsHub/negotiate**', route => {
    negotiated = true;
    return route.fulfill({ json: JSON.parse(negotiateResponse()) });
  });
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

  await page.goto(`/season/${SEASON_ID}/draft`);
  await expect(page.locator('mat-list-item.player-pool-item', { hasText: 'Pool Blair' })).toBeVisible();
  await expect.poll(() => sockets.length).toBeGreaterThan(0);

  const addCaptain = { seasonID: SEASON_ID, personID: 102, teamName: "Blair's Team", captainName: 'Pool Blair', orderNumber: 2 };
  captainAdded = true;
  sockets[sockets.length - 1].send(JSON.stringify({ type: 1, target: 'AddCaptain', arguments: [addCaptain] }) + RS);

  await expect(page.locator('mat-card.item:not(.dndPlaceholder) mat-card-title')).toHaveText(['1 - Red Team', "2 - Blair's Team"]);
  await expect(page.locator('mat-list-item.player-pool-item', { hasText: 'Pool Blair' })).toHaveCount(0);
  expect(negotiated, 'the client negotiated first').toBe(true);
});

test('the signup form ticks single slots and whole days, and submits the chosen option ids', async ({ page }) => {
  // Exactly what GET /api/seasons/{seasonID}/signups/mine/timeslots sends (GrifballWebApp.Server.Signups.TimeslotDto):
  // optionID, the day as the enum name, the time as "HH:mm:ss", isChecked.
  const timeslots = [
    { optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
    { optionID: 42, dayOfWeek: 'Monday', time: '20:00:00', isChecked: true },
    { optionID: 57, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false },
    { optionID: 58, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false },
  ];
  const posted: any[] = [];
  await mockBackend(page, {
    // GET: no signup yet (204 No Content); POST: the signup, saved (204)
    '/api/seasons/5/signups/mine': route => {
      if (route.request().method() === 'GET')
        return route.fulfill({ status: 204, body: '' });
      posted.push(route.request().postDataJSON());
      return route.fulfill({ status: 204, body: '' });
    },
    '/api/seasons/5/signups/mine/timeslots': route => json(route, timeslots),
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
  // Posted to season 5's signups (the only route answered); whose and which season are the server's
  expect(posted[0].seasonID).toBeUndefined();
  expect(posted[0].userID).toBeUndefined();
});

test('the Late League queue panel shows the count the hub sends, and joins from it', async ({ page }) => {
  // The status as GET /api/queue sends it (QueueStatusDto): signed in with a gamertag, not in the queue
  const status = { count: 6, open: true, signedIn: true, hasGamertag: true, inQueue: false, inMatch: false };
  await mockBackend(page, {
    '/api/queue': route => json(route, status),
    '/api/queue/join': route => json(route, { ...status, count: 10, inQueue: true }),
  });
  // QueueHub, as the team builder's hub above: its negotiate, then a WebSocket with the handshake answered
  await page.route('**/hub/QueueHub/negotiate**', route => route.fulfill({ json: JSON.parse(negotiateResponse()) }));
  const sockets: WebSocketRoute[] = [];
  await page.routeWebSocket(/\/hub\/QueueHub/, ws => {
    ws.onClose(() => sockets.splice(sockets.indexOf(ws), 1));
    ws.onMessage(message => {
      if (String(message).includes('"protocol"')) {
        ws.send(`{}${RS}`);
        sockets.push(ws);
      }
    });
  });

  await page.goto('/');
  const panel = page.locator('app-late-league-queue');
  await expect(panel.locator('.late-league-queue__number')).toHaveText('6');
  await expect.poll(() => sockets.length).toBeGreaterThan(0);

  // Someone joined elsewhere (Discord): the hub sends the new size (the server's status has it too, for a load the
  // connection's start may still have under way)
  status.count = 9;
  sockets[sockets.length - 1].send(JSON.stringify({ type: 1, target: 'QueueCount', arguments: [9] }) + RS);
  await expect(panel.locator('.late-league-queue__number')).toHaveText('9');

  await panel.getByRole('button', { name: 'Join the queue' }).click();
  await expect(panel.getByRole('button', { name: 'Leave the queue' })).toBeVisible();
  await expect(panel.locator('.late-league-queue__number')).toHaveText('10');
  await expect(panel.getByRole('status')).toHaveText('You have joined the Late League queue');
});

test('a commissioner enters a game by hand, and reports a Waypoint game in place of one', async ({ page }) => {
  const SEASON_MATCH_ID = 9;
  const WAYPOINT_ID = '7c9e6679-7425-40de-944b-e07fc1f90ae7';
  // A best of 3 with game 1 reported from Waypoint (SeasonMatchPageDto, ReportedGameDto)
  const line = (userID: number, gamertag: string) => ({ userID, gamertag, kills: 10, deaths: 8, assists: 2, bombScores: 1, score: 100 });
  const matchPage = {
    seasonID: 3, seasonName: 'Spring', isPlayoff: false,
    homeTeamName: 'Reds', homeTeamID: 1, homeTeamScore: 1, homeTeamResult: 'TBD',
    awayTeamName: 'Blues', awayTeamID: 2, awayTeamScore: 0, awayTeamResult: 'TBD',
    decidedBy: null, canUndo: true,
    scheduledTime: '2026-03-10T23:00:00Z', bestOf: 3,
    reportedGames: [{ seriesGameID: 1, matchNumber: 1, matchID: '0f8fad5b-d9cb-469f-a165-70867728950e', recordedGameID: null, homeScore: 5, awayScore: 2,
      homePlayers: [line(10, 'Sarge')], awayPlayers: [line(20, 'Tucker')], isManual: false }],
    seriesTotals: null, isImported: false, bracketInfo: null, activeRescheduleRequestId: null, canRequestReschedule: true,
  };
  const team = (teamID: number, teamName: string, gamertag: string) => ({ teamName, players: [
    { teamPlayerID: teamID * 10, userID: teamID * 10, gamertag, draftCaptainOrder: 1, draftRound: null }] });
  const possibleTeam = (teamID: number, score: number, gamertag: string) =>
    ({ teamID, score, outcome: score > 3 ? 'Won' : 'Lost', players: [{ xboxUserID: teamID, gamertag, score: 900, kills: 12, deaths: 7, isOnTeam: true }] });
  const posted: unknown[] = [];
  const requests = await mockBackend(page, {
    [`/api/season-matches/${SEASON_MATCH_ID}`]: route => json(route, matchPage),
    '/api/teams/1': route => json(route, team(1, 'Reds', 'Sarge')),
    '/api/teams/2': route => json(route, team(2, 'Blues', 'Tucker')),
    '/api/game-versions': route => json(route, [{ gameVersionID: 1, name: 'Halo 3' }, { gameVersionID: 6, name: 'Halo Infinite' }]),
    [`/api/season-matches/${SEASON_MATCH_ID}/possible-matches`]: route => json(route, [{ matchID: WAYPOINT_ID, homeTeam: possibleTeam(1, 5, 'Sarge'), awayTeam: possibleTeam(2, 3, 'Tucker') }]),
    [`/api/season-matches/${SEASON_MATCH_ID}/match-pull`]: route => json(route, { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null }),
    [`/api/season-matches/${SEASON_MATCH_ID}/manual-games`]: route => {
      posted.push(route.request().postDataJSON());
      return route.fulfill({ status: 200, body: '' });
    },
    [`/api/season-matches/${SEASON_MATCH_ID}/games/${WAYPOINT_ID}/report`]: route => json(route, 'Reported'),
  });
  const pageLoads = () => requests.filter(r => r === `GET /api/season-matches/${SEASON_MATCH_ID}`).length;

  await page.goto(`/seasonmatch/${SEASON_MATCH_ID}`);
  await page.getByRole('button', { name: 'Enter a game by hand' }).click();
  const form = page.getByRole('form', { name: 'Manual game entry' });
  // The series has a game: nothing is picked for the commissioner, so it cannot be saved yet
  await form.getByLabel('Reds score').fill('5');
  await form.getByLabel('Blues score').fill('3');
  await expect(form.getByRole('button', { name: 'Save game' })).toBeDisabled();
  await form.getByRole('combobox', { name: 'This game is' }).click();
  await page.getByRole('option', { name: 'A new game (game 2)' }).click();
  await form.getByRole('checkbox', { name: 'Sarge played' }).check();
  await form.getByRole('spinbutton', { name: 'Sarge kills' }).fill('12');
  await form.getByRole('button', { name: 'Save game' }).click();

  await expect.poll(() => posted.length).toBe(1);
  expect(posted[0]).toEqual({
    gameVersionID: 6, map: null, gametype: null, playedAt: null, homeTeamScore: 5, awayTeamScore: 3, replacesSeriesGameID: null,
    players: [{ userID: 10, isHomeTeam: true, kills: 12, deaths: 0, assists: 0, goals: 0, score: null }],
  });
  // Saved: the form closes and the page loads the match again
  await expect(form).toHaveCount(0);
  await expect.poll(pageLoads).toBe(2);

  // A Waypoint game reported to a series with games: the commissioner says it replaces game 1
  await page.locator('.lobby-possible').getByRole('button', { name: 'SUBMIT' }).click();
  const choice = page.getByRole('group', { name: 'Is this a new game, or does it replace one?' });
  await choice.getByRole('button', { name: 'Replaces game 1 (from Waypoint)' }).click();
  await expect.poll(() => requests).toContain(`POST /api/season-matches/${SEASON_MATCH_ID}/games/${WAYPOINT_ID}/report?replaces=1`);
  await expect(choice).toHaveCount(0);
});

test('the match page does one thing at a time: an action drops a game waiting to be placed, and every tool waits while one is sent', async ({ page }) => {
  const SEASON_MATCH_ID = 10;
  const WAYPOINT_ID = '7c9e6679-7425-40de-944b-e07fc1f90ae7';
  // A best of 3 with game 1 reported from Waypoint; once the home team forfeits, the server says so (decidedBy, canUndo)
  const line = (userID: number, gamertag: string) => ({ userID, gamertag, kills: 10, deaths: 8, assists: 2, bombScores: 1, score: 100 });
  const undecided = {
    seasonID: 3, seasonName: 'Spring', isPlayoff: false,
    homeTeamName: 'Reds', homeTeamID: 1, homeTeamScore: 1, homeTeamResult: 'TBD',
    awayTeamName: 'Blues', awayTeamID: 2, awayTeamScore: 0, awayTeamResult: 'TBD',
    decidedBy: null, canUndo: true,
    scheduledTime: '2026-03-10T23:00:00Z', bestOf: 3,
    reportedGames: [{ seriesGameID: 1, matchNumber: 1, matchID: '0f8fad5b-d9cb-469f-a165-70867728950e', recordedGameID: null, homeScore: 5, awayScore: 2,
      homePlayers: [line(10, 'Sarge')], awayPlayers: [line(20, 'Tucker')], isManual: false }],
    seriesTotals: null, isImported: false, bracketInfo: null, activeRescheduleRequestId: null, canRequestReschedule: true,
  };
  const forfeited = { ...undecided, homeTeamResult: 'Forfeit', awayTeamResult: 'Won', decidedBy: 'Forfeit' };
  let current: object = undecided;
  // The forfeit is answered only once the test has looked at the page while it is being sent
  let answer!: () => void;
  const answered = new Promise<void>(resolve => answer = resolve);
  const team = (teamID: number, teamName: string, gamertag: string) => ({ teamName, players: [
    { teamPlayerID: teamID * 10, userID: teamID * 10, gamertag, draftCaptainOrder: 1, draftRound: null }] });
  const possibleTeam = (teamID: number, score: number, gamertag: string) =>
    ({ teamID, score, outcome: score > 3 ? 'Won' : 'Lost', players: [{ xboxUserID: teamID, gamertag, score: 900, kills: 12, deaths: 7, isOnTeam: true }] });
  const requests = await mockBackend(page, {
    [`/api/season-matches/${SEASON_MATCH_ID}`]: route => json(route, current),
    '/api/teams/1': route => json(route, team(1, 'Reds', 'Sarge')),
    '/api/teams/2': route => json(route, team(2, 'Blues', 'Tucker')),
    '/api/game-versions': route => json(route, [{ gameVersionID: 6, name: 'Halo Infinite' }]),
    [`/api/season-matches/${SEASON_MATCH_ID}/possible-matches`]: route => json(route, [{ matchID: WAYPOINT_ID, homeTeam: possibleTeam(1, 5, 'Sarge'), awayTeam: possibleTeam(2, 3, 'Tucker') }]),
    [`/api/season-matches/${SEASON_MATCH_ID}/match-pull`]: route => json(route, { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null }),
    [`/api/season-matches/${SEASON_MATCH_ID}/forfeit/home`]: async route => {
      await answered;
      current = forfeited;
      return json(route, 'Forfeited');
    },
  });

  await page.goto(`/seasonmatch/${SEASON_MATCH_ID}`);
  const submit = page.locator('.lobby-possible').getByRole('button', { name: 'SUBMIT' });
  const choice = page.getByRole('group', { name: 'Is this a new game, or does it replace one?' });
  const prompt = page.getByText('The home team forfeits and the away team wins.');
  const homeForfeit = page.getByRole('button', { name: 'Home Team Forfeit' });
  const admin = page.locator('.lobby-admin');
  // Where focus goes when the control that had it is gone
  const heading = page.locator('#lobby-report');

  // A game waiting to be placed is dropped by asking for a forfeit, and the forfeit by reporting a game again
  await submit.click();
  await expect(choice).toBeVisible();
  await homeForfeit.click();
  await expect(prompt).toBeVisible();
  await expect(choice).toHaveCount(0);
  await expect(heading).toBeFocused();
  await submit.click();
  await expect(choice).toBeVisible();
  await expect(prompt).toHaveCount(0);
  // Cancel goes back to the SUBMIT that opened the choice; a forfeit's Cancel to its button, drawn again
  await choice.getByRole('button', { name: 'Cancel' }).click();
  await expect(choice).toHaveCount(0);
  await expect(submit).toBeFocused();
  await homeForfeit.click();
  await admin.getByRole('button', { name: 'Cancel' }).click();
  await expect(homeForfeit).toBeFocused();
  await expect(admin).toHaveAttribute('aria-busy', 'false');

  // While the forfeit is sent, nothing else can be: reporting, other actions, and entering or unreporting a game by hand
  await homeForfeit.click();
  await admin.getByRole('button', { name: 'Confirm' }).click();
  await expect.poll(() => requests).toContain(`POST /api/season-matches/${SEASON_MATCH_ID}/forfeit/home`);
  await expect(heading).toBeFocused();
  await expect(admin).toHaveAttribute('aria-busy', 'true');
  await expect(submit).toBeDisabled();
  await expect(homeForfeit).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Enter a game by hand' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Unreport game 1' })).toHaveCount(0);

  // Answered: the page loads the match again, decided as the server says
  answer();
  await expect(page.locator('main h1')).toHaveText('Match report');
  await expect(page.getByText('This match has been completed.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Undo Result' })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'Enter a game by hand' })).toHaveCount(0);
  await expect(admin).toHaveAttribute('aria-busy', 'false');
});
