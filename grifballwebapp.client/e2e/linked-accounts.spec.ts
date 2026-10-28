import { expect, test, type Page } from '@playwright/test';

// End-to-end linking of an external login from the profile page (/profile/:userID).
//
// The backend is mocked with page.route. The provider round trip is simulated:
// the full-page navigation to /api/ExternalLogins/Link answers with the redirect the
// real server ends in (provider -> /signin-discord -> /profile/:id?linkCallback=true).

const USER_ID = 21;

interface Login { loginProvider: string; providerKey: string; providerDisplayName: string }
interface Call { method: string; path: string; search: string; body: unknown }

async function openProfile(page: Page, start: { hasPassword: boolean; logins: Login[] }): Promise<Call[]> {
  const state = { ...start, logins: [...start.logins] };
  const calls: Call[] = [];

  await page.addInitScript((userID) => {
    localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'e2e-token', expiresIn: 3600, refreshToken: 'e2e-refresh' }));
    localStorage.setItem('metaInfo', JSON.stringify({ isSysAdmin: false, isCommissioner: false, isPlayer: true, displayName: 'E2E Player', userID }));
  }, USER_ID);

  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));

  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const json = (body: unknown) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    calls.push({ method: request.method(), path, search: url.search, body: request.postData() ? request.postDataJSON() : null });

    if (path === '/api/identity/metaInfo')
      return json({ isSysAdmin: false, isCommissioner: false, isPlayer: true, displayName: 'E2E Player', userID: USER_ID });
    if (path === `/api/profile/getgamertag/${USER_ID}`) return json({ gamertag: 'Sgt Grif' });
    if (path === '/api/ExternalLogins/Mine')
      return json({ ...state, providers: [{ name: 'Discord', displayName: 'Discord' }] });
    if (path === '/api/ExternalLogins/StartLink') return json({ token: 'signed-token' });
    if (path === '/api/ExternalLogins/Link')
      return route.fulfill({ status: 302, headers: { location: `/profile/${USER_ID}?linkCallback=true` } });
    if (path === '/api/ExternalLogins/LinkCallback') {
      const login = { loginProvider: 'Discord', providerKey: '555', providerDisplayName: 'Discord' };
      state.logins.push(login);
      return json(login);
    }
    if (path === '/api/ExternalLogins/Unlink') {
      const body = request.postDataJSON() as { providerKey: string };
      state.logins = state.logins.filter(l => l.providerKey !== body.providerKey);
      return route.fulfill({ status: 200, body: '' });
    }
    return route.fulfill({ status: 404, body: '' });
  });

  await page.goto(`/profile/${USER_ID}`);
  await expect(page.getByText('Gamertag: Sgt Grif')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Linked accounts' })).toBeVisible();
  return calls;
}

test('links Discord to the signed-in account through the provider round trip', async ({ page }) => {
  const calls = await openProfile(page, { hasPassword: true, logins: [] });
  await expect(page.getByText('No linked accounts.')).toBeVisible();

  await page.getByRole('button', { name: 'Link Discord' }).click();

  await expect(page.getByRole('status')).toHaveText('Linked Discord');
  await expect(page.locator('.linked-key')).toHaveText('555');
  // The callback query parameter is gone, so a reload does not try to link again.
  await expect(page).toHaveURL(new RegExp(`/profile/${USER_ID}$`));

  const link = calls.find(c => c.path === '/api/ExternalLogins/Link');
  expect(link?.search).toBe('?provider=Discord&token=signed-token');
  expect(calls.filter(c => c.path === '/api/ExternalLogins/LinkCallback').map(c => c.method)).toEqual(['POST']);
});

test('unlinks a login while a password remains', async ({ page }) => {
  const discord = { loginProvider: 'Discord', providerKey: '111', providerDisplayName: 'Discord' };
  await openProfile(page, { hasPassword: true, logins: [discord] });

  await page.getByRole('button', { name: 'Unlink' }).click();
  await expect(page.getByRole('status')).toHaveText('Unlinked Discord');
  await expect(page.getByText('No linked accounts.')).toBeVisible();
});

test('does not offer to unlink the only way to sign in', async ({ page }) => {
  const discord = { loginProvider: 'Discord', providerKey: '111', providerDisplayName: 'Discord' };
  await openProfile(page, { hasPassword: false, logins: [discord] });

  await expect(page.locator('.linked-key')).toHaveText('111');
  await expect(page.getByRole('button', { name: 'Unlink' })).toHaveCount(0);
  await expect(page.getByText('This is the only way to sign in')).toBeVisible();
});
