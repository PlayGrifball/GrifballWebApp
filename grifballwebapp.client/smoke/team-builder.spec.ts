import type { Page } from '@playwright/test';
import { drag, expect, hydrated, secondBrowser, test, watchConsole } from './fixtures';
import { createLeague, teamName, type League } from './league';

// The team builder (/season/:id/draft) against the real server, from two signed-in browsers at
// once: every drop is saved by the API, and the live hub (LiveHub) pushes the draft as it is now to the other browser,
// which shows it without reading it.

const teamCard = (page: Page, name: string) =>
  page.locator('mat-card.item:not(.dndPlaceholder)').filter({ has: page.locator('mat-card-title', { hasText: name }) });
const playersArea = (page: Page, name: string) => teamCard(page, name).locator('.players-area');
const teamsZone = (page: Page) => page.locator('div.dndList');
const poolItem = (page: Page, name: string) => page.locator('mat-list-item.player-pool-item', { hasText: name });

async function titles(page: Page): Promise<string[]> {
  return (await page.locator('mat-card.item:not(.dndPlaceholder) mat-card-title').allTextContents()).map(t => t.trim());
}
async function roster(page: Page, name: string): Promise<string[]> {
  return (await playersArea(page, name).locator('> div').allTextContents()).map(t => t.trim());
}
async function pool(page: Page): Promise<string[]> {
  return (await page.locator('mat-list-item.player-pool-item').allTextContents())
    .map(t => t.trim()).filter(t => t !== '***').sort();
}
/** Everything the page shows of the draft, to compare two browsers. */
async function draft(page: Page): Promise<{ titles: string[]; rosters: string[][]; pool: string[] }> {
  const cards = page.locator('mat-card.item:not(.dndPlaceholder)');
  const rosters: string[][] = [];
  for (const card of await cards.all()) {
    rosters.push((await card.locator('.players-area > div').allTextContents()).map(t => t.trim()));
  }
  return { titles: await titles(page), rosters, pool: await pool(page) };
}

/**
 * Resolves once the page watches `topic` on the live hub: the server's answer (a completion) to a Watch that names it,
 * on a WebSocket of the hub. Any of them: the app makes its connection again as the signed-in user once the session is
 * known (hubLifecycle), which on a preview behind Cloudflare can close the first socket before it is answered. The app
 * logs nothing when it connects.
 */
async function watching(page: Page, topic: string, timeout: number): Promise<void> {
  type Message = { type?: number, invocationId?: string, target?: string, arguments?: unknown[] };
  // JSON Hub Protocol messages, each ending in the record separator; anything else is not one of ours
  const messages = (payload: string | Buffer): Message[] => String(payload).split('\u001e').filter(m => m.trim())
    .flatMap(m => {
      try {
        return [JSON.parse(m) as Message];
      } catch {
        return [];
      }
    });
  await new Promise<void>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`Not watching ${topic} on the live hub within ${timeout} ms`)), timeout);
    page.on('websocket', ws => {
      if (!/\/hub\/LiveHub/i.test(ws.url()))
        return;
      const asked = new Set<string>();
      ws.on('framesent', frame => {
        for (const m of messages(frame.payload))
          if (m.type === 1 && m.target === 'Watch' && m.invocationId !== undefined && (m.arguments?.[0] as string[]).includes(topic))
            asked.add(m.invocationId);
      });
      ws.on('framereceived', frame => {
        if (messages(frame.payload).some(m => m.type === 3 && m.invocationId !== undefined && asked.has(m.invocationId))) {
          clearTimeout(timer);
          resolve();
        }
      });
    });
  });
}

/** Opens the team builder; `hub: false` for a page whose live hub is cut off on purpose. */
async function openTeamBuilder(page: Page, league: League, { hub = true } = {}): Promise<void> {
  // Changes reach the other browser over the live hub: one made before it watches the draft is
  // never sent to it (seen in CI: the first drop came 0.6 s before the other page connected).
  const connected = hub && watching(page, `draft:${league.seasonID}`, 30_000);
  await page.goto(`/season/${league.seasonID}/draft`);
  await expect(teamCard(page, teamName(league, 0))).toBeVisible();
  // The last player is never drafted: the pool has loaded.
  await expect(poolItem(page, league.players[league.players.length - 1].name)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Lock Captains' })).toBeVisible(); // i.e. unlocked
  // The server drew the board too; drags only work once the app has drawn it again.
  await hydrated(page);
  await connected;
}

test.describe('team builder', () => {
  let league: League;

  test.beforeAll(async ({ baseUrl, admin }) => {
    // p1, p2 captain "Team 1" and "Team 2"; p3..p7 wait in the pool.
    league = await createLeague(baseUrl, admin, 'teams', { players: 7, captains: 2 });
    await admin.post('unlockCaptains', `seasons/${league.seasonID}/draft/captains/unlock`);
  });
  test.afterAll(async () => league?.close());

  test('drafts, makes a captain, reorders, moves a pick, refuses a stale pick, and both browsers agree', async ({ adminPage: a, consoleErrors, browser, smoke, admin }) => {
    const [, , p3, p4, p5] = league.players;
    const team1 = teamName(league, 0);
    const team2 = teamName(league, 1);
    const p4Team = `${p4.name}'s Team`; // p4 signed up without a team name.

    // The draft helper asks for suggestions each time the draft changes; the server answers 404 when no team is on deck
    // (TeamsController.NoTeamOnDeck), and the browser logs that request.
    const noTeamOnDeck = /status of 404.*\/draft\/suggestions/i;
    consoleErrors.allow(noTeamOnDeck);
    const other = await secondBrowser(browser, smoke[1]);
    other.consoleErrors.allow(noTeamOnDeck);
    const b = other.page;
    try {
      await openTeamBuilder(a, league);
      await openTeamBuilder(b, league);
      await b.evaluate(() => { (window as unknown as { notReloaded: boolean }).notReloaded = true; });
      // The other browser's reads of the draft from here on: none for a draft the live hub pushes
      const bLoads: string[] = [];
      b.on('request', r => { if (r.method() === 'GET' && /\/api\/seasons\/\d+\/draft\/teams/i.test(r.url())) bLoads.push(r.url()); });

      // 1. Draft a pool player onto a team. The live hub pushes the draft as it is now: the other browser
      //    shows the pick without reading the draft, and without a page reload.
      const pickSaved = a.waitForResponse(r => /\/api\/seasons\/\d+\/draft\/picks/i.test(r.url()));
      await drag(poolItem(a, p3.name), playersArea(a, team1));
      await expect.poll(() => roster(a, team1)).toEqual([`1 ${p3.name}`]);
      await expect(poolItem(a, p3.name)).toHaveCount(0);
      expect((await pickSaved).ok()).toBe(true);
      await expect.poll(() => roster(b, team1)).toEqual([`1 ${p3.name}`]);
      await expect(poolItem(b, p3.name)).toHaveCount(0);
      expect(await b.evaluate(() => (window as unknown as { notReloaded?: boolean }).notReloaded)).toBe(true);
      expect(bLoads, 'the pushed draft is shown as it came, with no read').toEqual([]);

      // 2. A pool player dropped among the teams becomes a captain, of a team named after them.
      await drag(poolItem(a, p4.name), teamsZone(a));
      await expect(teamCard(a, p4Team)).toBeVisible();
      await expect(teamCard(a, p4Team).locator('mat-card-subtitle')).toHaveText(`Captain: ${p4.name}`);
      await expect(teamCard(b, p4Team)).toBeVisible();

      // 3. Reorder captains: drop the new team on the leading edge of the first one.
      await drag(teamCard(a, p4Team), teamCard(a, team1), { x: 5, y: 20 });
      await expect.poll(() => titles(a)).toEqual([`1 - ${p4Team}`, `2 - ${team1}`, `3 - ${team2}`]);
      await expect.poll(() => titles(b)).toEqual([`1 - ${p4Team}`, `2 - ${team1}`, `3 - ${team2}`]);

      // 4. Move a pick from one team to another.
      await drag(playersArea(a, team1).locator('> div', { hasText: p3.name }), playersArea(a, team2));
      await expect.poll(() => roster(a, team2)).toEqual([`1 ${p3.name}`]);
      await expect.poll(() => roster(a, team1)).toEqual([]);
      await expect.poll(() => roster(b, team2)).toEqual([`1 ${p3.name}`]);

      // 5. A browser that missed an update (its hub blocked) drafts a player someone else already
      //    took: the server refuses, the page says why and puts the real draft back.
      const stale = await other.context.newPage();
      const staleErrors = watchConsole(stale);
      // Its hub can't connect, on purpose.
      staleErrors.allow(/hub\/(LiveHub|ChatHub)|WebSocket|negotiat|Failed to complete negotiation|Error: Failed to start/i);
      // The refused save: the browser logs the failed request.
      staleErrors.allow(/status of (400|409|500).*\/draft\/picks/i);
      staleErrors.allow(noTeamOnDeck);
      await stale.route('**/hub/**', route => route.abort());
      await stale.routeWebSocket(/\/hub\//, ws => ws.close());
      await openTeamBuilder(stale, league, { hub: false });

      // Saved before the stale browser tries: otherwise its request could win the race.
      const saved = a.waitForResponse(r => /\/api\/seasons\/\d+\/draft\/picks/i.test(r.url()));
      await drag(poolItem(a, p5.name), playersArea(a, team2));
      expect((await saved).ok()).toBe(true);
      await expect.poll(() => roster(a, team2)).toEqual([`1 ${p3.name}`, `2 ${p5.name}`]);
      await expect(poolItem(stale, p5.name)).toBeVisible(); // stale

      await drag(poolItem(stale, p5.name), playersArea(stale, team1));
      await expect(stale.getByText('Draft change not saved: This player is already on a team')).toBeVisible();
      await expect.poll(() => roster(stale, team1)).toEqual([]);
      await expect.poll(() => roster(stale, team2)).toEqual([`1 ${p3.name}`, `2 ${p5.name}`]);
      await expect(poolItem(stale, p5.name)).toHaveCount(0);

      // 6. Everyone agrees with the server: the live browser without reloading, the first after a reload.
      const server = await admin.get('getTeams', `seasons/${league.seasonID}/draft/teams`);
      expect(server.map(t => [t.captain.order, t.teamName]).sort()).toEqual([[1, p4Team], [2, team1], [3, team2]]);
      expect(server.find(t => t.teamName === team2)!.players.map(p => p.name)).toEqual([p3.name, p5.name]);

      const live = await draft(b);
      await a.reload();
      await openTeamBuilder(a, league);
      await expect.poll(() => draft(a)).toEqual(live);
      await expect.poll(() => draft(stale)).toEqual(live);

      expect(other.consoleErrors.unexpected(), 'second browser console errors').toEqual([]);
      expect(staleErrors.unexpected(), 'stale browser console errors').toEqual([]);
    } finally {
      await other.context.close();
    }
  });
});
