import type { Page } from '@playwright/test';
import type { TimeslotDto } from './api';
import { expect, test } from './fixtures';
import { createLeague, type League } from './league';

// The signup form (/season/:id/signupForm): ticking single slots, the day and time headers (which
// toggle: tick all when none is ticked, else untick all), submitting, and what the server keeps.

// The app shows times in US Eastern (it offsets from America/Detroit); in that zone the slots show
// unshifted, so the grid is exactly the season's Mon-Wed x 19:00-22:00.
test.use({ timezoneId: 'America/Detroit' });

const table = (page: Page) => page.locator('app-availability-table table');
const row = (page: Page, day: string) => table(page).locator('tbody tr').filter({ has: page.locator('td', { hasText: day }) });
const box = (page: Page, day: string, column: number) =>
  row(page, day).locator('td').nth(column).locator('input[type="checkbox"]');
const timeHeader = (page: Page, time: string) => table(page).locator('thead th button', { hasText: time });

/** Ticked cells as "Day column" (columns 1-4 are 7, 8, 9, 10 PM). */
async function ticked(page: Page): Promise<string[]> {
  const result: string[] = [];
  for (const day of ['Monday', 'Tuesday', 'Wednesday']) {
    for (let column = 1; column <= 4; column++) {
      if (await box(page, day, column).isChecked()) result.push(`${day} ${column}`);
    }
  }
  return result;
}

const key = (t: { dayOfWeek: string; time: string }) => `${t.dayOfWeek} ${t.time}`;

test.describe('signup form', () => {
  let league: League;

  test.beforeAll(async ({ baseUrl, admin }) => {
    league = await createLeague(baseUrl, admin, 'signup');
  });
  test.afterAll(async () => league?.close());

  test('ticks single slots and whole days and times, submits by optionID, and reloads what was saved', async ({ adminPage: page, admin }) => {
    await page.goto(`/season/${league.seasonID}/signupForm`);
    await expect(table(page).locator('tbody tr')).toHaveCount(3);
    await expect(table(page).locator('thead th')).toHaveText(['Day Of Week', '7:00 PM', '8:00 PM', '9:00 PM', '10:00 PM']);
    expect(await ticked(page)).toEqual([]);

    // A single slot.
    await box(page, 'Monday', 1).click();
    await expect.poll(() => ticked(page)).toEqual(['Monday 1']);

    // A time header with nothing ticked in its column ticks the whole column.
    await timeHeader(page, '9:00 PM').click();
    await expect.poll(() => ticked(page)).toEqual(['Monday 1', 'Monday 3', 'Tuesday 3', 'Wednesday 3']);

    // A day header with something ticked unticks the day; clicked again it ticks all of it.
    await row(page, 'Wednesday').locator('td button').first().click();
    await expect.poll(() => ticked(page)).toEqual(['Monday 1', 'Monday 3', 'Tuesday 3']);
    await row(page, 'Wednesday').locator('td button').first().click();
    const expected = ['Monday 1', 'Monday 3', 'Tuesday 3', 'Wednesday 1', 'Wednesday 2', 'Wednesday 3', 'Wednesday 4'];
    await expect.poll(() => ticked(page)).toEqual(expected);

    const upsert = page.waitForRequest(r => /\/api\/Signups\/UpsertSignup/i.test(r.url()) && r.method() === 'POST');
    await page.getByRole('button', { name: 'Submit' }).click();
    const sent = (await upsert).postDataJSON() as { seasonID: number; timeslots: TimeslotDto[] };
    await expect(page.getByText('Signup Submitted')).toBeVisible();

    // The payload names slots by the season's optionIDs.
    const options = await admin.get<TimeslotDto[]>(`Signups/GetTimeslots/${league.seasonID}?offset=0`);
    const optionID = new Map(options.map(o => [key(o), o.optionID]));
    const wanted = ['Monday 19:00:00', 'Monday 21:00:00', 'Tuesday 21:00:00',
      'Wednesday 19:00:00', 'Wednesday 20:00:00', 'Wednesday 21:00:00', 'Wednesday 22:00:00'];
    expect(sent.seasonID).toBe(league.seasonID);
    const sentTicked = sent.timeslots.filter(t => t.isChecked);
    expect(sentTicked.map(t => t.optionID).sort()).toEqual(wanted.map(w => optionID.get(w)).sort());
    expect(sentTicked.map(key).sort()).toEqual([...wanted].sort());

    // The server kept exactly those, and the form shows them again after a reload.
    const saved = await admin.get<{ timeslots: TimeslotDto[] }>(`Signups/GetSignup/${league.seasonID}?offset=0`);
    expect(saved.timeslots.filter(t => t.isChecked).map(key).sort()).toEqual([...wanted].sort());

    await page.reload();
    await expect(table(page).locator('tbody tr')).toHaveCount(3);
    await expect.poll(() => ticked(page)).toEqual(expected);
  });
});
