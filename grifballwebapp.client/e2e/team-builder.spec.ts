import { expect, test, type Locator, type Page } from '@playwright/test';

// End-to-end drag and drop on the team builder (/season/:seasonID/teams).
//
// The backend is fully mocked with page.route and synthetic data. Every drop
// in the team builder is persisted immediately by a POST to /api/Teams/*, so
// the tests assert both what the page shows and the payload it sends.
// ngx-drag-drop uses native HTML5 drag and drop. The drag() helper below uses real
// pointer input (mouse down, stepped moves, mouse up); Playwright's Chromium turns
// that into native dragstart/dragover/drop events carrying a real DataTransfer, so
// no events are synthesised. (locator.dragTo() works as well.)

const SEASON_ID = 7;

interface Player { name: string; personID: number; pick: number | null; round: number | null }
interface Team { teamID: number; teamName: string; captain: { name: string; personID: number; order: number }; players: Player[] }
interface Call { path: string; body: unknown }

const player = (personID: number, name: string, round: number | null = null): Player =>
  ({ name, personID, pick: round, round });

function seed(): { teams: Team[]; pool: Player[] } {
  return {
    teams: [
      { teamID: 1, teamName: 'Red Team', captain: { name: 'Captain Red', personID: 201, order: 1 }, players: [player(301, 'Player Pat', 1), player(302, 'Player Quinn', 2)] },
      { teamID: 2, teamName: 'Blue Team', captain: { name: 'Captain Blue', personID: 202, order: 2 }, players: [] },
    ],
    pool: [player(101, 'Pool Alex'), player(102, 'Pool Blair'), player(103, 'Pool Casey')],
  };
}

/** Logs in as a synthetic commissioner and mocks every API and SignalR call the page makes. */
async function openTeamBuilder(page: Page, opts: { captainsLocked?: boolean } = {}): Promise<Call[]> {
  const data = seed();
  const calls: Call[] = [];

  await page.addInitScript(() => {
    localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'e2e-token', expiresIn: 3600, refreshToken: 'e2e-refresh' }));
    localStorage.setItem('metaInfo', JSON.stringify({ isSysAdmin: false, isCommissioner: true, isPlayer: false, displayName: 'E2E Organizer', userID: 999 }));
  });

  // Keep the run hermetic: index.html pulls Google Fonts and a jsDelivr script that the
  // team builder doesn't need, and a slow CDN would otherwise stall the page's load event.
  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());

  // The Teams SignalR hub is not available: negotiation fails, the page logs it and
  // keeps working (the hub only pushes other users' changes).
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));

  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const json = (body: unknown) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

    if (path === '/api/identity/metaInfo') {
      return json({ isSysAdmin: false, isCommissioner: true, isPlayer: false, displayName: 'E2E Organizer', userID: 999 });
    }
    if (path === `/api/Teams/GetTeams/${SEASON_ID}`) return json(data.teams);
    if (path === `/api/Teams/GetPlayerPool/${SEASON_ID}`) return json(data.pool);
    if (path === `/api/Teams/areCaptainsLocked/${SEASON_ID}`) return json(opts.captainsLocked ?? true);
    if (path.startsWith('/api/Teams/') && request.method() === 'POST') {
      calls.push({ path, body: request.postDataJSON() });
      return json({});
    }
    // Anything else (toolbar commit info, sidebar, ...) is irrelevant here.
    return route.fulfill({ status: 404, body: '' });
  });

  await page.goto(`/season/${SEASON_ID}/teams`);
  await expect(teamCard(page, 'Red Team')).toBeVisible();
  await expect(poolItem(page, 'Pool Alex')).toBeVisible();
  return calls;
}

const teamsZone = (page: Page) => page.locator('div.dndList');
const poolZone = (page: Page) => page.locator('mat-list.dndList');
const teamCard = (page: Page, teamName: string) =>
  page.locator('mat-card.item:not(.dndPlaceholder)').filter({ has: page.locator('mat-card-title', { hasText: teamName }) });
const playersArea = (page: Page, teamName: string) => teamCard(page, teamName).locator('.players-area');
const teamPlayer = (page: Page, teamName: string, name: string) => playersArea(page, teamName).locator('> div', { hasText: name });
const poolItem = (page: Page, name: string) => page.locator('mat-list-item.player-pool-item', { hasText: name });

async function teamTitles(page: Page): Promise<string[]> {
  return (await page.locator('mat-card.item:not(.dndPlaceholder) mat-card-title').allTextContents()).map(t => t.trim());
}
async function rosterOf(page: Page, teamName: string): Promise<string[]> {
  return (await playersArea(page, teamName).locator('> div').allTextContents()).map(t => t.trim());
}
async function poolNames(page: Page): Promise<string[]> {
  return (await page.locator('mat-list-item.player-pool-item').allTextContents()).map(t => t.trim()).filter(t => t !== '***');
}

/**
 * Real pointer drag: press on the source, move in small steps, release over the target
 * (its centre, or `at` relative to its top-left corner). Chromium turns this into native
 * dragstart/dragenter/dragover/drop events with a real DataTransfer.
 */
async function drag(source: Locator, target: Locator, at?: { x: number; y: number }): Promise<void> {
  const from = (await source.boundingBox())!;
  const page = source.page();
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2 + 5, from.y + from.height / 2 + 5, { steps: 5 });
  const to = (await target.boundingBox())!;
  await page.mouse.move(to.x + (at?.x ?? to.width / 2), to.y + (at?.y ?? to.height / 2), { steps: 10 });
  await page.mouse.up();
}

async function waitForCalls(calls: Call[], count: number): Promise<void> {
  await expect.poll(() => calls.length, { message: `expected ${count} API call(s), got ${JSON.stringify(calls)}` }).toBe(count);
}

test('drafts a pool player onto a team', async ({ page }) => {
  const calls = await openTeamBuilder(page);

  await drag(poolItem(page, 'Pool Blair'), playersArea(page, 'Blue Team'));

  await expect.poll(() => rosterOf(page, 'Blue Team')).toEqual(['1 Pool Blair']);
  await expect.poll(() => poolNames(page)).toEqual(['Pool Alex', 'Pool Casey']);
  await waitForCalls(calls, 1);
  expect(calls[0]).toEqual({ path: '/api/Teams/addPlayerToTeam/', body: { seasonID: SEASON_ID, captainID: 202, personID: 102 } });
});

test('moves a drafted player from one team to another', async ({ page }) => {
  const calls = await openTeamBuilder(page);

  await drag(teamPlayer(page, 'Red Team', 'Player Quinn'), playersArea(page, 'Blue Team'));

  await expect.poll(() => rosterOf(page, 'Blue Team')).toEqual(['1 Player Quinn']);
  await expect.poll(() => rosterOf(page, 'Red Team')).toEqual(['1 Player Pat']);
  await waitForCalls(calls, 1);
  expect(calls[0]).toEqual({
    path: '/api/Teams/movePlayerToTeam/',
    body: { seasonID: SEASON_ID, previousCaptainID: 201, newCaptainID: 202, personID: 302, roundNumber: 1 },
  });
});

test('moves a pick to the end of its own team (intended: picks are not reordered mid-list)', async ({ page }) => {
  const calls = await openTeamBuilder(page);

  // Intended behaviour: re-dropping a pick on its own team moves it to the last round.
  // The players area deliberately has no placeholder, so a drop never carries an
  // insertion index and the pick is appended.
  await drag(teamPlayer(page, 'Red Team', 'Player Pat'), playersArea(page, 'Red Team'));

  await expect.poll(() => rosterOf(page, 'Red Team')).toEqual(['1 Player Quinn', '2 Player Pat']);
  await waitForCalls(calls, 1);
  expect(calls[0]).toEqual({
    path: '/api/Teams/movePlayerToTeam/',
    body: { seasonID: SEASON_ID, previousCaptainID: 201, newCaptainID: 201, personID: 301, roundNumber: 2 },
  });
});

test('returns a drafted player to the pool', async ({ page }) => {
  const calls = await openTeamBuilder(page);

  await drag(teamPlayer(page, 'Red Team', 'Player Pat'), poolZone(page));

  await expect.poll(() => rosterOf(page, 'Red Team')).toEqual(['1 Player Quinn']);
  await expect.poll(() => poolNames(page)).toContain('Player Pat');
  await waitForCalls(calls, 1);
  expect(calls[0]).toEqual({ path: '/api/Teams/removePlayerFromTeam/', body: { seasonID: SEASON_ID, captainID: 201, personID: 301 } });
});

test('makes a pool player a captain when captains are unlocked', async ({ page }) => {
  const calls = await openTeamBuilder(page, { captainsLocked: false });

  await drag(poolItem(page, 'Pool Casey'), teamsZone(page));

  await expect.poll(() => teamTitles(page)).toEqual(['1 - Red Team', '2 - Blue Team', "3 - Pool Casey's Team"]);
  await expect.poll(() => poolNames(page)).toEqual(['Pool Alex', 'Pool Blair']);
  await waitForCalls(calls, 1);
  expect(calls[0]).toEqual({ path: '/api/Teams/AddCaptain/', body: { seasonID: SEASON_ID, personID: 103, orderNumber: 3 } });
});

test('reorders the captains\' pick order when captains are unlocked', async ({ page }) => {
  const calls = await openTeamBuilder(page, { captainsLocked: false });

  // Drop Blue on the leading edge of Red, i.e. in front of it.
  await drag(teamCard(page, 'Blue Team'), teamCard(page, 'Red Team'), { x: 5, y: 20 });

  await expect.poll(() => teamTitles(page)).toEqual(['1 - Blue Team', '2 - Red Team']);
  await waitForCalls(calls, 1);
  expect(calls[0]).toEqual({ path: '/api/Teams/ResortCaptain/', body: { seasonID: SEASON_ID, personID: 202, orderNumber: 1 } });
});

test('returns a captain and their picks to the pool when captains are unlocked', async ({ page }) => {
  const calls = await openTeamBuilder(page, { captainsLocked: false });

  await drag(teamCard(page, 'Red Team'), poolZone(page));

  await expect.poll(() => teamTitles(page)).toEqual(['1 - Blue Team']);
  await expect.poll(async () => (await poolNames(page)).sort()).toEqual(['Captain Red', 'Player Pat', 'Player Quinn', 'Pool Alex', 'Pool Blair', 'Pool Casey']);
  await waitForCalls(calls, 1);
  expect(calls[0]).toEqual({ path: '/api/Teams/RemoveCaptain/', body: { seasonID: SEASON_ID, personID: 201 } });
});

test('does not let teams be dragged while captains are locked', async ({ page }) => {
  const calls = await openTeamBuilder(page, { captainsLocked: true });

  await expect(teamCard(page, 'Blue Team')).toHaveAttribute('draggable', 'false');
  await drag(teamCard(page, 'Blue Team'), teamCard(page, 'Red Team'));

  await expect.poll(() => teamTitles(page)).toEqual(['1 - Red Team', '2 - Blue Team']);
  expect(calls).toEqual([]);
});
