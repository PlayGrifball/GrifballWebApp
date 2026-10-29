import { expect, test, type Page } from '@playwright/test';
import { META, mockResponse } from './mockApi.mts';

// Signing in and out with the session cookie. The backend is mocked with page.route and keeps a
// session like the API: login "sets the cookie" (the mock's state), Me answers who it belongs to,
// and every change must carry the antiforgery token of the current session.

interface Call { method: string; path: string; xsrf?: string; authorization?: string }

async function mockBackend(page: Page): Promise<Call[]> {
  const calls: Call[] = [];
  let signedIn = false;
  const token = () => signedIn ? 'xsrf-signed-in' : 'xsrf-visitor';

  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));
  await page.route('**/api/**', route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const headers = request.headers();
    calls.push({ method: request.method(), path, xsrf: headers['x-xsrf-token'], authorization: headers['authorization'] });
    const json = (body: unknown) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

    if (path === '/api/Identity/Me')
      return signedIn ? json(META.player) : route.fulfill({ status: 204, body: '' });
    if (path === '/api/Identity/Antiforgery')
      return json({ token: token() });
    if (request.method() !== 'GET' && headers['x-xsrf-token'] !== token())
      return route.fulfill({ status: 400, headers: { 'X-Antiforgery-Rejected': 'true' }, body: '' });
    if (path === '/api/identity/login') {
      signedIn = true;
      return route.fulfill({ status: 200, body: '' });
    }
    if (path === '/api/Identity/Logout') {
      signedIn = false;
      return route.fulfill({ status: 204, body: '' });
    }
    const mocked = mockResponse('anon', path);
    return route.fulfill(mocked ?? { status: 404, body: '' });
  });
  return calls;
}

test('tokens an earlier version kept in localStorage are removed, and not used', async ({ page }) => {
  await page.addInitScript(() => {
    // Only on the first load: the app must remove them, not this script
    if (sessionStorage.getItem('seeded'))
      return;
    sessionStorage.setItem('seeded', '1');
    localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'old-token', expiresIn: 3600, refreshToken: 'old-refresh' }));
    localStorage.setItem('metaInfo', JSON.stringify({ isSysAdmin: true, isCommissioner: true, isPlayer: true, displayName: 'Old User', userID: 1 }));
  });
  const calls = await mockBackend(page);

  await page.goto('/');
  await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();

  expect(await page.evaluate(() => [localStorage.getItem('access_token'), localStorage.getItem('metaInfo')])).toEqual([null, null]);
  // A visitor, as the API says: nothing from the old storage
  await expect(page.locator('.hud-user')).toHaveCount(0);
  expect(calls.filter(c => c.authorization)).toEqual([]);
});

test('signs in with a password and out again, with the antiforgery token and no bearer token', async ({ page }) => {
  const calls = await mockBackend(page);

  await page.goto('/login');
  await page.locator('input[name="username"]').fill('chief');
  await page.locator('input[name="password"]').fill('pw');
  await page.getByRole('button', { name: 'Submit' }).click();

  await expect(page.locator('.hud-user')).toContainText(META.player.displayName);
  const login = calls.find(c => c.path === '/api/identity/login')!;
  expect(login.xsrf).toBe('xsrf-visitor');

  await page.getByRole('button', { name: 'Logout' }).click();
  await expect(page.locator('.hud-user')).toHaveCount(0);
  // The token belongs to who it was made for: a new one after signing in
  const logout = calls.find(c => c.path === '/api/Identity/Logout')!;
  expect(logout.method).toBe('POST');
  expect(logout.xsrf).toBe('xsrf-signed-in');

  expect(calls.filter(c => c.authorization)).toEqual([]);
  expect(calls.filter(c => c.path === '/api/Identity/Refresh')).toEqual([]);
});

test('the user chip opens a menu: Profile, and Log out, which goes home signed out', async ({ page }) => {
  const calls = await mockBackend(page);
  await page.goto('/login');
  await page.locator('input[name="username"]').fill('chief');
  await page.locator('input[name="password"]').fill('pw');
  await page.getByRole('button', { name: 'Submit' }).click();

  const chip = page.locator('button.hud-user');
  await expect(chip).toContainText(META.player.displayName);
  await expect(chip).toHaveAttribute('aria-haspopup', 'menu');
  await expect(chip).toHaveAttribute('aria-expanded', 'false');

  // By keyboard: opens with the focus on Profile; the arrows move; Esc closes back to the chip
  await chip.focus();
  await page.keyboard.press('Enter');
  await expect(chip).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByRole('menuitem', { name: 'Profile' })).toBeFocused();
  await expect(page.getByRole('menuitem', { name: 'Profile' })).toHaveAttribute('href', `/profile/${META.player.userID}`);
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('menuitem', { name: 'Log out' })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('menu')).toHaveCount(0);
  await expect(chip).toBeFocused();

  await chip.click();
  await page.getByRole('menuitem', { name: 'Log out' }).click();
  await expect(page.locator('.hud-user')).toHaveCount(0);
  await expect(page).toHaveURL(/\/$/);
  expect(calls.find(c => c.path === '/api/Identity/Logout')?.xsrf).toBe('xsrf-signed-in');
});
