import { expect, test, type Page } from '@playwright/test';
import { mockResponse, signIn, type Role } from './mockApi.mts';

// Angular keeps the page when a link only changes the ID in its route (/profile/117 -> /profile/999): each of these
// pages must then show the new one, not keep the previous one. The backend is mocked (e2e/mockApi.mts); `extra` adds
// or replaces answers for one test.

async function mockBackend(page: Page, role: Role, extra: Record<string, unknown> = {}): Promise<void> {
  await signIn(page.context(), role);
  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    if (url.pathname in extra)
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(extra[url.pathname]) });
    const mocked = mockResponse(role, url.pathname, url.search);
    return route.fulfill(mocked ? { status: mocked.status, contentType: mocked.contentType, body: mocked.body } : { status: 404, body: '' });
  });
}

test('Profile in the header, on someone else\'s profile, shows your own profile', async ({ page }) => {
  await mockBackend(page, 'admin');
  await page.goto('/profile/117');
  const name = page.locator('#profile-name');
  await expect(name).toHaveText('MasterChief117');

  await page.locator('button.hud-user').click();
  await page.getByRole('menuitem', { name: 'Profile' }).click();

  await expect(page).toHaveURL(/\/profile\/999$/);
  await expect(name).toHaveText('CommanderGrif');
  await expect(page.locator('.profile-facts')).toContainText('999');
  // Your own profile: your linked accounts, which someone else's profile does not show
  await expect(page.locator('app-linked-accounts')).toBeVisible();
});

test('a link from one season to another shows the other season', async ({ page }) => {
  // Wide enough for the Current Season link to be in the toolbar, not the menu
  await page.setViewportSize({ width: 1600, height: 900 });
  await mockBackend(page, 'player');
  await page.goto('/season/5');
  const title = page.locator('app-season h1');
  await expect(title).toHaveText('Summer League 2010');
  await expect(page.locator('app-backfilled-banner')).toBeVisible();

  await page.locator('.toolbar-links a.nav-link', { hasText: 'Current Season' }).click();

  await expect(page).toHaveURL(/\/season\/12$/);
  await expect(title).toHaveText('Autumn Brawl');
  await expect(page.locator('app-team-standings')).toContainText('Red Rampage');
  await expect(page.locator('app-backfilled-banner')).toHaveCount(0);
  await expect(page.locator('app-season-tabs a').first()).toHaveAttribute('href', '/season/12');
});

test('Winner Next Match on a playoff match shows the next match', async ({ page }) => {
  const base = JSON.parse(mockResponse('anon', '/api/season-matches/31')!.body);
  const bracketMatch = (homeTeamID: number, homeTeamName: string, awayTeamID: number, awayTeamName: string, bracketInfo: object) => ({
    ...base, isPlayoff: true, homeTeamID, homeTeamName, awayTeamID, awayTeamName, homeTeamScore: 0, awayTeamScore: 0,
    homeTeamResult: 'TBD', awayTeamResult: 'TBD', reportedGames: [], seriesTotals: null,
    bracketInfo: { homeTeamSeed: null, awayTeamSeed: null, homeTeamPreviousMatchID: null, awayTeamPreviousMatchID: null,
      winnerNextMatchID: null, loserNextMatchID: null, isGrandFinal: false, ...bracketInfo },
  });
  const pullStatus = JSON.parse(mockResponse('anon', '/api/season-matches/31/match-pull')!.body);
  await mockBackend(page, 'anon', {
    // A semi-final whose winner goes on to the final
    '/api/season-matches/105': bracketMatch(1, 'Red Rampage', 5, 'Gravity Wells', { homeTeamSeed: 1, awayTeamSeed: 5, winnerNextMatchID: 107 }),
    '/api/season-matches/107': bracketMatch(2, 'Blue Bombers', 6, 'Sticky Situation', { homeTeamPreviousMatchID: 105, awayTeamPreviousMatchID: 106 }),
    '/api/season-matches/105/possible-matches': [],
    '/api/season-matches/107/possible-matches': [],
    '/api/season-matches/105/match-pull': pullStatus,
    '/api/season-matches/107/match-pull': pullStatus,
    '/api/season-matches/105/vods': [],
    '/api/season-matches/107/vods': [],
  });
  await page.goto('/seasonmatch/105');
  const match = page.locator('app-season-match');
  await expect(match).toContainText('Gravity Wells');

  await page.getByRole('link', { name: 'Winner Next Match' }).click();

  await expect(page).toHaveURL(/\/seasonmatch\/107$/);
  await expect(match).toContainText('Sticky Situation');
  await expect(match).not.toContainText('Gravity Wells');
  await expect(page.getByRole('link', { name: 'Home Team Previous Match' })).toHaveAttribute('href', '/seasonmatch/105');
});
