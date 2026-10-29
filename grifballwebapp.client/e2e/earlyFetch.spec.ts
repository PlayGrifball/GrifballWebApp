import { expect, test, type Page } from '@playwright/test';
import { META, mockResponse, type Role } from './mockApi.mts';

// index.html starts the first page's API calls before the app has loaded; the app then uses those
// answers (src/app/earlyFetch.interceptor.ts). Each call must still go out only once, and a failed
// early call must not leave the page empty. The backend is mocked (e2e/mockApi.mts).

/** Mocks the backend as `role` and counts the API calls by path and query. */
async function mockBackend(page: Page, role: Role, failFirst: string[] = []): Promise<Map<string, number>> {
  const calls = new Map<string, number>();
  if (role !== 'anon') {
    await page.addInitScript(meta => {
      localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'e2e-token', expiresIn: 3600, refreshToken: 'e2e-refresh' }));
      localStorage.setItem('metaInfo', JSON.stringify(meta));
    }, META[role]);
  }
  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    const key = url.pathname + url.search;
    const count = (calls.get(key) ?? 0) + 1;
    calls.set(key, count);
    if (count === 1 && failFirst.includes(key))
      return route.fulfill({ status: 503, body: '' });
    const mocked = mockResponse(role, url.pathname);
    return route.fulfill(mocked ? { status: mocked.status, contentType: mocked.contentType, body: mocked.body } : { status: 404, body: '' });
  });
  return calls;
}

const PAGES = [
  { path: '/', role: 'anon', ready: 'tr.mat-mdc-row', early: ['/api/Home/CurrentAndFutureEvents/', '/api/Home/PastSeasons?pageSize=10&pageNumber=1'] },
  { path: '/season/12', role: 'player', ready: '.week-card', early: ['/api/Season/getSeasonName/12', '/api/TeamStandings/GetTeamStandings/12', '/api/MatchPlanner/GetScheduledMatches/12', '/api/identity/metaInfo'] },
  { path: '/season/12?tab=schedule', role: 'anon', ready: '.sched-match', early: ['/api/MatchPlanner/GetUnscheduledMatches/12', '/api/MatchPlanner/GetScheduledMatches/12'] },
  { path: '/season/12?tab=bracket', role: 'anon', ready: '.brackets-viewer .match', early: ['/api/Brackets/GetViewerData?seasonID=12'] },
  { path: '/seasonmatch/31', role: 'anon', ready: '.lobby-player', early: ['/api/SeasonMatch/GetSeasonMatchPage/31', '/api/SeasonMatch/GetPossibleMatches/31'] },
  { path: '/profile/117', role: 'player', ready: '.linked-login', early: ['/api/profile/getgamertag/117'] },
] as const;

for (const { path, role, ready, early } of PAGES) {
  test(`${path} fetches its data once, starting before the app has loaded`, async ({ page }) => {
    const calls = await mockBackend(page, role);
    await page.goto(path);
    await expect(page.locator(ready).first()).toBeVisible();

    // The app's own calls on every page, and this page's
    for (const url of ['/api/Season/getCurrentSeasonID/', '/api/CommitHash', '/api/CommitDate', ...early])
      expect(calls.get(url), url).toBe(1);
    // The inline script fetched them, and the app used its answers (each is dropped once used)
    expect(await page.evaluate(() => typeof window.grifEarly?.at)).toBe('number');
    expect(await page.evaluate(urls => urls.filter(url => url in window.grifEarly!.responses), [...early])).toEqual([]);
  });
}

test('the app asks again when an early call fails', async ({ page }) => {
  const calls = await mockBackend(page, 'anon', ['/api/Home/CurrentAndFutureEvents/']);
  await page.goto('/');

  await expect(page.locator('.home-hero h1')).toBeVisible();
  await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();
  expect(calls.get('/api/Home/CurrentAndFutureEvents/')).toBe(2);
});

declare global {
  interface Window { grifEarly?: { at: number, responses: Record<string, unknown> } }
}
