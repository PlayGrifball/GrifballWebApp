import { expect, test } from './fixtures';

// Merge users with an ID that doesn't exist: the page says so, without an error, and won't merge.
// Nothing is merged: the button must stay disabled.

test('merge users: an unknown ID shows "User not found." and the merge stays disabled', async ({ adminPage: page, admin }) => {
  const missing = 2_000_000_000; // Far above any real user ID.

  await page.goto('/usermanagement/mergeuser');
  const from = page.locator('app-search-box').nth(0).locator('input');
  const to = page.locator('app-search-box').nth(1).locator('input');

  const lookup = page.waitForResponse(r => r.url().toLowerCase().includes(`/getuser/${missing}`));
  await from.fill(String(missing));
  await to.fill(String(admin.userID)); // A real user (the smoke user), so only "from" is unknown.
  expect((await lookup).ok()).toBe(true); // 204, no body.

  await expect(page.getByText('User not found.')).toHaveCount(1);
  await expect(page.locator('.merge-error, [role="alert"]')).toHaveCount(0);
  await expect(page.locator('mat-snack-bar-container')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Merge Users' })).toBeDisabled();
});
