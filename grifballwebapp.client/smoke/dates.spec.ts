import { expect, test } from './fixtures';
import { createLeague, createLockedOutUser, type League } from './league';
import type { UserResponseDto } from './api';

// Dates through the date pickers: the season editor shows the season's dates, and saving a user
// with a permanent lockout (DateTimeOffset.MaxValue) without touching it keeps it.

// A zone ahead of UTC: there, DateTimeOffset.MaxValue used to come back from the browser as a
// date past year 9999, which the server refused.
test.use({ timezoneId: 'Europe/Berlin' });

test.describe('dates', () => {
  let league: League;

  test.beforeAll(async ({ baseUrl, admin }) => {
    league = await createLeague(baseUrl, admin, 'dates');
  });
  test.afterAll(async () => league?.close());

  test('the season editor shows the season\'s dates', async ({ adminPage: page }) => {
    await page.goto(`/seasonEdit/${league.seasonID}`);
    await expect(page.locator('input[name="seasonName"]')).toHaveValue(league.name);

    const thisYear = String(new Date().getFullYear());
    const expected: Record<string, RegExp> = {
      // Signups opened yesterday and close in a week (either may fall in the next year).
      signupsOpen: new RegExp(`${thisYear}|${Number(thisYear) - 1}`),
      signupsClose: new RegExp(`${thisYear}|${Number(thisYear) + 1}`),
      // league.ts: draft 2099-01-05, season 2099-01-10 to 2099-03-01.
      draftStart: /1\/5\/2099/,
      seasonStart: /1\/10\/2099/,
      seasonEnd: /3\/1\/2099/,
    };
    for (const [name, pattern] of Object.entries(expected)) {
      const input = page.locator(`input[name="${name}"]`);
      await expect(input, name).toHaveValue(pattern);
      await expect(input, name).not.toHaveValue(/invalid/i);
    }

    // The season list shows them too.
    await page.goto('/seasonManager');
    const row = page.locator('tr', { hasText: league.name });
    await expect(row).toContainText('2099');
    await expect(row).not.toContainText(/invalid/i);
  });

  test('a permanent lockout survives saving the user with only a new display name', async ({ adminPage: page, admin, baseUrl }) => {
    const user = await createLockedOutUser(baseUrl, admin, 'dates-lockout');
    expect(user.lockoutEnd).toMatch(/^9999-12-31/);

    await page.goto(`/usermanagement/edituser/${user.userID}`);
    await expect(page.locator('input[name="userName"]')).toHaveValue(user.userName);
    const lockout = page.locator('input[name="lockoutEnd"]');
    await expect(lockout).toHaveValue(/9999/);
    await expect(lockout).not.toHaveValue(/invalid/i);

    const displayName = `${user.userName} renamed`;
    await page.locator('input[name="displayName"]').fill(displayName);
    const saved = page.waitForResponse(r => /\/api\/UserManagement\/EditUser/i.test(r.url()));
    await page.getByRole('button', { name: 'Update User' }).click();
    expect((await saved).status()).toBe(200);

    const after = await admin.get<UserResponseDto>(`UserManagement/GetUser/${user.userID}`);
    expect(after.displayName).toBe(displayName);
    expect(after.lockoutEnd).toBe(user.lockoutEnd);
  });
});
