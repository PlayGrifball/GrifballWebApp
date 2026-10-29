import { expect, test, type Page } from '@playwright/test';
import { META, mockResponse, type Role } from './mockApi.mts';

// Code that is no longer in the app's first download but loaded when it is first needed: the theme
// dialog, the snack bar (with its default 5 s duration) and the date-time pickers with their Luxon
// adapter. Each must still work as before. The backend is mocked (e2e/mockApi.mts).

async function mockBackend(page: Page, role: Role): Promise<void> {
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
    if (url.pathname === '/api/identity/login')
      return route.fulfill({ status: 401, body: '' });
    const mocked = mockResponse(role, url.pathname);
    return route.fulfill(mocked ? { status: mocked.status, contentType: mocked.contentType, body: mocked.body } : { status: 404, body: '' });
  });
}

test('the theme editor opens in a dialog', async ({ page }) => {
  await mockBackend(page, 'anon');
  await page.goto('/');
  await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();

  await page.getByRole('button', { name: 'Theme icon' }).click();

  await expect(page.locator('mat-dialog-container app-theme')).toBeVisible();
});

test('a failed login says so in a snack bar that closes itself after 5 seconds', async ({ page }) => {
  await mockBackend(page, 'anon');
  await page.goto('/login');
  await page.locator('input[name="username"]').fill('grif');
  await page.locator('input[name="password"]').fill('wrong');
  await page.locator('.login-panel button.hud-btn--primary').click();

  const snackBar = page.locator('mat-snack-bar-container');
  await expect(snackBar).toContainText('Login failed');
  await expect(snackBar).toBeHidden({ timeout: 10_000 });
});

test('the schedule editor shows match times in its date-time pickers', async ({ page }) => {
  await mockBackend(page, 'admin');
  await page.goto('/season/12?tab=schedule');
  await expect(page.locator('.sched-match').first()).toBeVisible();

  await page.getByRole('button', { name: 'Show Edit' }).click();

  // The Luxon adapter's yyyy-LL-dd HH:mm, as before
  await expect(page.getByRole('textbox', { name: 'Match time' }).first()).toHaveValue(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/);
  await page.locator('mtx-datetimepicker-toggle button').first().click();
  await expect(page.locator('mtx-datetimepicker-content')).toBeVisible();
});
