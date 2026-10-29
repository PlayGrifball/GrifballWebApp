import { expect, test, type Page } from '@playwright/test';
import { META } from './mockApi.mts';

// The public pages are rendered on the server (src/server.ts, src/app/app.routes.server.ts) and
// then taken over by the app in the browser (hydration). On localhost, which the server renders
// for, with its API answered from e2e/mockApi.mts as a visitor (e2e/serve.mjs).

const SITE = `http://localhost:${process.env.E2E_PORT ?? 4300}`;

const PAGES = [
  { path: '/', content: '.home-hero h1', text: 'Winter Campaign', data: ['/api/Home/CurrentAndFutureEvents/', '/api/Home/PastSeasons'] },
  { path: '/season/12', content: 'app-team-standings', text: 'Red Rampage', data: ['/api/Season/getSeasonName/12', '/api/TeamStandings/GetTeamStandings/12', '/api/MatchPlanner/GetScheduledMatches/12'] },
  { path: '/season/12?tab=schedule', content: '.sched-match', text: 'Gravity Wells', data: ['/api/MatchPlanner/GetScheduledMatches/12', '/api/MatchPlanner/GetUnscheduledMatches/12'] },
  { path: '/seasonmatch/31', content: '.lobby-player', text: 'Blue Bombers', data: ['/api/SeasonMatch/GetSeasonMatchPage/31'] },
  { path: '/profile/117', content: '.profile-card', text: 'MasterChief117', data: ['/api/profile/getgamertag/117'] },
] as const;

/** Keeps the run hermetic, and lists the API calls the browser makes and what it logs as an error. */
async function watch(page: Page): Promise<{ calls: string[], errors: string[] }> {
  const calls: string[] = [];
  const errors: string[] = [];
  await page.route(url => url.hostname !== 'localhost', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));
  page.on('request', request => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/api/'))
      calls.push(url.pathname);
  });
  page.on('console', message => {
    if (message.type() === 'error')
      errors.push(message.text());
  });
  page.on('pageerror', error => errors.push(error.message));
  return { calls, errors };
}

for (const { path, content, text } of PAGES) {
  test(`${path} has its content in the page the server sends`, async ({ browser }) => {
    // Without scripts, only what the server rendered is there
    const context = await browser.newContext({ javaScriptEnabled: false });
    const page = await context.newPage();
    const response = await page.goto(SITE + path);

    expect(response?.status()).toBe(200);
    await expect(page.locator(content).first()).toBeVisible();
    await expect(page.locator('main')).toContainText(text);
    await context.close();
  });
}

for (const { path, content, data } of PAGES) {
  test(`${path} is taken over by the app without errors or fetching its data again`, async ({ page }) => {
    const { calls, errors } = await watch(page);
    await page.goto(SITE + path);
    await expect(page.locator(content).first()).toBeVisible();
    await page.waitForLoadState('networkidle');

    // The server's answers came with the page (HttpClient's transfer cache)
    for (const url of data)
      expect(calls, url).not.toContain(url);
    // No hydration mismatch (NG05xx) or any other error
    expect(errors).toEqual([]);
  });
}

test('a signed-in user gets the visitor\'s page, then their own parts, without errors', async ({ page }) => {
  await page.addInitScript(meta => {
    localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'e2e-token', expiresIn: 3600, refreshToken: 'e2e-refresh' }));
    localStorage.setItem('metaInfo', JSON.stringify(meta));
  }, META.admin);
  const { calls, errors } = await watch(page);
  await page.route('**/api/identity/metaInfo', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(META.admin) }));

  await page.goto(SITE + '/seasonmatch/31');

  // Their name in the header and the commissioner tools, which the server left out
  await expect(page.locator('.hud-user__name')).toHaveText(META.admin.displayName);
  await expect(page.getByRole('button', { name: 'Home Team Forfeit' })).toBeVisible();
  await page.waitForLoadState('networkidle');
  expect(calls).toContain('/api/identity/metaInfo');
  expect(calls).not.toContain('/api/SeasonMatch/GetSeasonMatchPage/31');
  expect(errors).toEqual([]);
});

test('an unknown page is the not-found page, with status 404', async ({ page }) => {
  const response = await page.goto(SITE + '/no-such-page');
  expect(response?.status()).toBe(404);
});

test('pages for signed-in users only render in the browser', async ({ browser }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage();
  await page.goto(SITE + '/usermanagement');
  // The page that loads the app: the boot shell, no content
  await expect(page.locator('.boot-shell')).toBeAttached();
  await expect(page.locator('app-user-management')).toHaveCount(0);
  await context.close();
});

test('the server passes /api on to the backend, without its prefix', async ({ request }) => {
  const response = await request.get(SITE + '/api/Season/getCurrentSeasonID/');
  expect(response.status()).toBe(200);
  expect(await response.json()).toBe(12);
});

test('another host name gets the page that renders in the browser', async ({ request }) => {
  // As a probe on the pod's address does
  const response = await request.get(`http://127.0.0.1:${process.env.E2E_PORT ?? 4300}/season/12`);
  expect(response.status()).toBe(200);
  const html = await response.text();
  expect(html).toContain('boot-shell');
  expect(html).not.toMatch(/<app-root[^>]*ng-server-context/);
});

test('the build\'s files are compressed and kept for good; the page is checked on every visit', async ({ request }) => {
  const page = await request.get(SITE + '/', { headers: { 'Accept-Encoding': 'gzip' } });
  expect(page.headers()['cache-control']).toBe('no-cache');
  expect(page.headers()['content-encoding']).toBe('gzip');
  const script = /src="(main-[A-Za-z0-9_-]{8}\.js)"/.exec(await page.text())![1];

  const main = await request.get(`${SITE}/${script}`, { headers: { 'Accept-Encoding': 'gzip' } });
  expect(main.headers()['cache-control']).toBe('public, max-age=31536000, immutable');
  expect(main.headers()['content-encoding']).toBe('gzip');
});
