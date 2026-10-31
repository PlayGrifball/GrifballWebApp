import { expect, test, type Page } from '@playwright/test';
import { META, SESSION_COOKIE, signIn } from './mockApi.mts';

// The public pages are rendered on the server (src/server.ts, src/app/app.routes.server.ts) and
// then taken over by the app in the browser (hydration). On localhost, which the server renders
// for, with its API answered from e2e/mockApi.mts (e2e/serve.mjs): as a visitor, or as the role
// the session cookie names.

const SITE = `http://localhost:${process.env.E2E_PORT ?? 4300}`;

const PAGES = [
  { path: '/', content: '.home-hero h1', text: 'Winter Campaign', data: ['/api/Home/CurrentAndFutureEvents/', '/api/Home/PastSeasons'] },
  { path: '/season/12', content: 'app-team-standings', text: 'Red Rampage', data: ['/api/Season/getSeasonName/12', '/api/TeamStandings/GetTeamStandings/12', '/api/MatchPlanner/GetScheduledMatches/12'] },
  { path: '/season/12?tab=schedule', content: '.sched-match', text: 'Gravity Wells', data: ['/api/MatchPlanner/GetScheduledMatches/12', '/api/MatchPlanner/GetUnscheduledMatches/12'] },
  { path: '/seasonmatch/31', content: '.lobby-player', text: 'Blue Bombers', data: ['/api/SeasonMatch/GetSeasonMatchPage/31'] },
  { path: '/profile/117', content: '.profile-card', text: 'MasterChief117', data: ['/api/profile/getgamertag/117', '/api/profile/getprofilestats/117'] },
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

const cookie = (role: 'player' | 'admin') => ({ Cookie: `theme=x; ${SESSION_COOKIE}=${role}` });
/** The name in the header of a rendered page, or null for a visitor's. */
const headerName = (html: string) => /class="hud-user__name"[^>]*>\s*([^<]*?)\s*</.exec(html)?.[1] ?? null;

test('a signed-in user\'s page is rendered as theirs, and is private and never cached', async ({ request }) => {
  const response = await request.get(SITE + '/seasonmatch/31', { headers: cookie('admin') });

  expect(response.status()).toBe(200);
  const html = await response.text();
  // Their name in the header and the commissioner tools, in the HTML itself
  expect(headerName(html)).toBe(META.admin.displayName);
  expect(html).toContain('Home Team Forfeit');
  expect(response.headers()['cache-control']).toBe('private, no-store');
  expect(response.headers()['vary']).toMatch(/(^|,\s*)Cookie(,|$)/i);
});

test('a visitor\'s page has no one\'s name, and is checked on every visit as before', async ({ request }) => {
  const response = await request.get(SITE + '/seasonmatch/31');

  const html = await response.text();
  expect(html).toContain('Blue Bombers');
  expect(headerName(html)).toBeNull();
  expect(html).not.toContain(META.admin.displayName);
  expect(html).not.toContain('Home Team Forfeit');
  expect(response.headers()['cache-control']).toBe('no-cache');
  // Varies by Cookie too: a cache that kept it must not hand it to someone signed in
  expect(response.headers()['vary']).toMatch(/(^|,\s*)Cookie(,|$)/i);
});

test('two users each get their own page', async ({ request }) => {
  const [player, admin] = await Promise.all([
    request.get(SITE + '/seasonmatch/31', { headers: cookie('player') }).then(r => r.text()),
    request.get(SITE + '/seasonmatch/31', { headers: cookie('admin') }).then(r => r.text()),
  ]);

  expect(headerName(player)).toBe(META.player.displayName);
  expect(player).not.toContain(META.admin.displayName);
  expect(player).not.toContain('Home Team Forfeit');
  expect(headerName(admin)).toBe(META.admin.displayName);
  expect(admin).toContain('Home Team Forfeit');
});

test('a signed-in user\'s page is taken over without the header changing, asking again or errors', async ({ page }) => {
  await signIn(page.context(), 'admin');
  // Notes any time the signed-in header leaves the page (as it would if the app drew a visitor's first)
  await page.addInitScript(() => {
    const seen = { removed: 0 };
    (window as unknown as { userHeader: typeof seen }).userHeader = seen;
    new MutationObserver(records => {
      for (const record of records)
        for (const node of record.removedNodes)
          if (node instanceof Element && (node.matches('.hud-user') || node.querySelector('.hud-user')))
            seen.removed++;
    }).observe(document, { childList: true, subtree: true });
  });
  const { calls, errors } = await watch(page);

  await page.goto(SITE + '/seasonmatch/31');

  await expect(page.locator('.hud-user__name')).toHaveText(META.admin.displayName);
  await expect(page.getByRole('button', { name: 'Home Team Forfeit' })).toBeVisible();
  await page.waitForLoadState('networkidle');
  expect(await page.evaluate(() => (window as unknown as { userHeader: { removed: number } }).userHeader.removed)).toBe(0);
  // Who is signed in came with the page, as did the page's data
  expect(calls).not.toContain('/api/Identity/Me');
  expect(calls).not.toContain('/api/SeasonMatch/GetSeasonMatchPage/31');
  expect(errors).toEqual([]);
});

test('an unknown page is the not-found page, with status 404', async ({ page }) => {
  const response = await page.goto(SITE + '/no-such-page');
  expect(response?.status()).toBe(404);
});

test('pages for signed-in users are rendered on the server too, as that user', async ({ browser }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  await signIn(context, 'admin');
  const page = await context.newPage();

  const response = await page.goto(SITE + '/usermanagement');

  await expect(page.locator('app-user-management')).toBeAttached();
  await expect(page.locator('app-user-management')).toContainText('MasterChief117');
  expect(response?.headers()['cache-control']).toBe('private, no-store');
  await context.close();
});

for (const path of ['/seasonManager', '/usermanagement', '/usermanagement/createuser', '/season/13/signupForm', '/season/12/teams', '/reschedule-request/31']) {
  test(`${path} is rendered for a signed-in commissioner and taken over without errors`, async ({ page }) => {
    await signIn(page.context(), 'admin');
    const { errors } = await watch(page);

    const response = await page.goto(SITE + path);

    expect(response?.status()).toBe(200);
    expect(response?.headers()['cache-control']).toBe('private, no-store');
    expect(await response!.text()).toMatch(/<app-root[^>]*ng-server-context/);
    await expect(page.locator('.hud-user__name')).toHaveText(META.admin.displayName);
    await page.waitForLoadState('networkidle');
    // Only what the mock API leaves out (answers it has no data for, the SignalR hub); no hydration
    // mismatch (NG05xx) or other error
    expect(errors.filter(e => !/Failed to load resource|negotiation|Failed to start the connection/.test(e))).toEqual([]);
  });
}

test('a visitor gets no signed-in page, and nothing private', async ({ request }) => {
  const response = await request.get(SITE + '/usermanagement');

  expect(response.headers()['cache-control']).toBe('no-cache');
  const html = await response.text();
  expect(headerName(html)).toBeNull();
  expect(html).not.toContain('MasterChief117');
});

test('the theme page renders in the browser', async ({ browser }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage();
  await page.goto(SITE + '/theme');
  // The page that loads the app: the boot shell, no content
  await expect(page.locator('.boot-shell')).toBeAttached();
  await expect(page.locator('app-theme')).toHaveCount(0);
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

// Sign up is offered only while a season's signups are open, the same in the page the server sends
// and once the browser has taken it over: the Winter Campaign (13) has open signups in the mock
// API, the Autumn Brawl (12) none.
for (const [seasonID, offered] of [[13, true], [12, false]] as const) {
  test(`/season/${seasonID} ${offered ? 'offers' : 'does not offer'} Sign up, on the server and in the browser`, async ({ page, request }) => {
    const html = await (await request.get(SITE + '/season/' + seasonID)).text();
    expect(/>\s*Sign up\s*</.test(html)).toBe(offered);

    const { errors } = await watch(page);
    await page.goto(SITE + '/season/' + seasonID);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('.season-head').getByRole('link', { name: 'Sign up' })).toHaveCount(offered ? 1 : 0);
    expect(errors.filter(e => /NG0\d|hydration/i.test(e))).toEqual([]);
  });
}

// The nav has no Login link; a visitor on a phone signs in from the header's Login button, one tap.
test('a visitor on a phone reaches the login page in one tap from the header', async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 360, height: 640 } });
  const page = await context.newPage();
  const { errors } = await watch(page);
  await page.goto(SITE + '/');
  await expect(page.locator('.toolbar-links a.nav-link', { hasText: 'Login' })).toHaveCount(0);
  const login = page.locator('header').getByRole('link', { name: 'Login icon' });
  await expect(login).toBeVisible();
  await expect(login).toBeInViewport();
  await login.click();
  await expect(page).toHaveURL(SITE + '/login');
  await expect(page.locator('.login-panel input[name="username"]')).toBeVisible();
  expect(errors.filter(e => /NG0\d|hydration/i.test(e))).toEqual([]);
  await context.close();
});
