import type { BrowserContext, Page } from '@playwright/test';
import { Api, type UserResponseDto } from './api';
import { expect, headerName, hydrated, login, secondBrowser, test, withoutScripts } from './fixtures';
import { createLeague, newUser, smokeTag, type League } from './league';

// The cookie session and the pages the server renders for it, on the real server: the session
// cookie, logging out, a visitor's and a signed-in user's page as the server sends them, two users
// side by side, antiforgery, the theme picker, and the possible matches a visitor sees.

const SESSION_COOKIE = '__Host-Grifball';

const sessionCookie = async (context: BrowserContext) =>
  (await context.cookies()).find(c => c.name === SESSION_COOKIE);

/** What the app shows as this user's name (no display name: the user name). */
async function shownName(admin: { get<T>(path: string): Promise<T> }, userID: number): Promise<string> {
  const user = await admin.get<UserResponseDto>(`UserManagement/GetUser/${userID}`);
  return user.displayName || user.userName;
}

test.describe('session', () => {
  let league: League;
  let seasonMatchID: number;

  test.beforeAll(async ({ baseUrl, admin }) => {
    // Two teams, one match between them
    league = await createLeague(baseUrl, admin, 'session', { players: 2, captains: 2 });
    await admin.get(`MatchPlanner/CreateSeasonMatches?seasonID=${league.seasonID}&homeMatchesPerTeam=1&bestOf=1`);
    const matches = await admin.get<{ seasonMatchID: number }[]>(`MatchPlanner/GetUnscheduledMatches/${league.seasonID}`);
    expect(matches.length).toBeGreaterThan(0);
    seasonMatchID = matches[0].seasonMatchID;
  });
  test.afterAll(async () => league?.close());

  test('signing in sets an HttpOnly, Secure session cookie; logging out clears it', async ({ page, smoke, consoleErrors }) => {
    void consoleErrors;
    await login(page, smoke[0]);
    const cookie = await sessionCookie(page.context());
    expect(cookie, 'the session cookie').toBeDefined();
    expect(cookie!.httpOnly).toBe(true);
    expect(cookie!.secure).toBe(true);
    expect((await page.request.get('/api/Identity/Me')).status()).toBe(200);

    await page.goto('/login');
    await hydrated(page);
    const loggedOut = page.waitForResponse(r => /\/api\/Identity\/Logout/i.test(r.url()));
    await page.getByRole('button', { name: 'Logout' }).click();
    expect((await loggedOut).ok()).toBe(true);

    await expect.poll(() => sessionCookie(page.context())).toBeUndefined();
    expect((await page.request.get('/api/Identity/Me')).status()).toBe(204);
  });

  test('logging out from the user chip\'s menu ends the session and goes home', async ({ page, smoke, consoleErrors }) => {
    void consoleErrors;
    await login(page, smoke[0]);
    await page.goto(`/season/${league.seasonID}`);
    const chip = page.locator('button.hud-user');
    await chip.click();
    await expect(chip).toHaveAttribute('aria-expanded', 'true');
    await expect(page.getByRole('menuitem', { name: 'Profile' })).toBeFocused();

    const loggedOut = page.waitForResponse(r => /\/api\/Identity\/Logout/i.test(r.url()));
    await page.getByRole('menuitem', { name: 'Log out' }).click();
    expect((await loggedOut).ok()).toBe(true);

    await expect(page).toHaveURL(/\/$/);
    await expect(page.locator('.hud-user')).toHaveCount(0);
    await expect(page.locator('header').getByRole('link', { name: 'Login icon' })).toBeVisible();
    await expect.poll(() => sessionCookie(page.context())).toBeUndefined();
    expect((await page.request.get('/api/Identity/Me')).status()).toBe(204);
  });

  test('a visitor gets the page with its content in the HTML', async ({ playwright, baseUrl }) => {
    // A plain request: no browser, no cookie
    const http = await playwright.request.newContext({ baseURL: baseUrl });
    try {
      const response = await http.get(`/season/${league.seasonID}`);
      expect(response.status()).toBe(200);
      const html = withoutScripts(await response.text());
      expect(html).toContain(league.name);
      expect(headerName(html)).toBeNull();
      expect(response.headers()['cache-control']).not.toMatch(/private/);
    } finally {
      await http.dispose();
    }
  });

  test('a signed-in user gets their own page, rendered with their name and kept private', async ({ page, smoke, admin, playwright, baseUrl, consoleErrors }) => {
    void consoleErrors;
    await login(page, smoke[0]);
    const cookie = await sessionCookie(page.context());
    const name = await shownName(admin, admin.userID);

    const http = await playwright.request.newContext({ baseURL: baseUrl });
    try {
      const response = await http.get(`/season/${league.seasonID}`, { headers: { Cookie: `${SESSION_COOKIE}=${cookie!.value}` } });
      expect(response.status()).toBe(200);
      const html = withoutScripts(await response.text());
      expect(headerName(html)).toBe(name);
      expect(html).toContain(league.name);
      expect(response.headers()['cache-control']).toBe('private, no-store');
    } finally {
      await http.dispose();
    }
  });

  test('two users side by side each see only their own name, also after a reload', async ({ page, smoke, browser, baseUrl, admin, consoleErrors }) => {
    void consoleErrors;
    const second = await Api.login(baseUrl, smoke[1]);
    const names = [await shownName(admin, admin.userID), await shownName(admin, second.userID)];
    await second.dispose();
    expect(names[0]).not.toBe(names[1]);

    await login(page, smoke[0]);
    const other = await secondBrowser(browser, smoke[1]);
    try {
      const pages = [page, other.page];
      for (const path of ['/', `/season/${league.seasonID}`, `/seasonmatch/${seasonMatchID}`]) {
        for (const reload of [false, true]) {
          for (const [i, p] of pages.entries()) {
            if (reload)
              await p.reload();
            else
              await p.goto(path);
            await expect(p.locator('.hud-user__name'), `${path} for user ${i + 1}`).toHaveText(names[i]);
          }
        }
        // As the server drew it for each of them, side by side
        for (const [i, p] of pages.entries()) {
          const response = await p.request.get(path);
          expect(headerName(withoutScripts(await response.text())), `${path} drawn for user ${i + 1}`).toBe(names[i]);
        }
      }
      expect(other.consoleErrors.unexpected()).toEqual([]);
    } finally {
      await other.context.close();
    }
  });

  test('a commissioner\'s change carries the antiforgery token; without it the same request is refused and changes nothing', async ({ adminPage: page, admin, baseUrl }) => {
    const { user, api } = await newUser(baseUrl, admin, `smoke-${smokeTag()}-xsrf`);
    await api.dispose();

    await page.goto(`/usermanagement/edituser/${user.userID}`);
    await expect(page.locator('input[name="userName"]')).toHaveValue(user.userName);
    await hydrated(page);
    const displayName = `${user.userName} ok`;
    await page.locator('input[name="displayName"]').fill(displayName);
    const sent = page.waitForRequest(r => /\/api\/UserManagement\/EditUser/i.test(r.url()) && r.method() === 'POST');
    await page.getByRole('button', { name: 'Update User' }).click();
    const request = await sent;
    expect((await request.allHeaders())['x-xsrf-token'], 'X-XSRF-TOKEN').toBeTruthy();
    expect((await request.response())?.status()).toBe(200);
    const saved = await admin.get<UserResponseDto>(`UserManagement/GetUser/${user.userID}`);
    expect(saved.displayName).toBe(displayName);

    // The same change with the browser's cookies (session and antiforgery) but no token
    const forged = await page.request.post('/api/UserManagement/EditUser', { data: { ...saved, displayName: `${user.userName} bad` } });
    expect([400, 403]).toContain(forged.status());
    const after = await admin.get<UserResponseDto>(`UserManagement/GetUser/${user.userID}`);
    expect(after.displayName).toBe(displayName);
  });

  test('the theme picker applies a colour, and "Reset to Spartan HUD" puts the default back', async ({ page, consoleErrors }) => {
    void consoleErrors;
    const variable = (name: string) => page.evaluate(n => getComputedStyle(document.documentElement).getPropertyValue(n).trim().toLowerCase(), name);
    await page.goto('/theme');
    const picker = page.locator('app-palette').first().locator('input');
    await expect(picker).toBeVisible();
    await expect.poll(() => variable('--primary')).not.toBe('');
    const defaults = { seed: await variable('--primary'), shade: await variable('--primary-40') };

    await picker.fill('#ff3366');
    await picker.press('Enter');
    await expect.poll(() => variable('--primary')).toBe('#ff3366');
    await expect.poll(() => variable('--primary-40')).not.toBe(defaults.shade);

    await page.getByRole('button', { name: 'Reset to Spartan HUD' }).click();
    await expect.poll(() => variable('--primary')).toBe(defaults.seed);
    await expect.poll(() => variable('--primary-40')).toBe(defaults.shade);
  });

  test('a visitor sees the possible matches on a match page', async ({ page, playwright, baseUrl, consoleErrors }) => {
    void consoleErrors;
    // In the page the server sends, under way (the server never looks: it is slow)
    const http = await playwright.request.newContext({ baseURL: baseUrl });
    try {
      const html = withoutScripts(await (await http.get(`/seasonmatch/${seasonMatchID}`)).text());
      expect(html).toContain('Possible Matches');
      expect(html).toContain('Looking for games');
    } finally {
      await http.dispose();
    }

    await page.goto(`/seasonmatch/${seasonMatchID}`);
    const section = page.locator('section.lobby-possibles');
    await expect(section.getByRole('heading', { name: 'Possible Matches' })).toBeVisible();
    // Then the list, none, or (previews have no Halo Infinite credentials) the search failing; never stuck
    await expect(section).not.toContainText('Looking for games', { timeout: 60_000 });
    await expect(section).toContainText(/No games found for these teams yet\.|Could not look for games right now\.|: \d+ - \d+/);
    // Reporting a game is for the organizers
    await expect(section.getByRole('button', { name: 'SUBMIT' })).toHaveCount(0);
  });
});
