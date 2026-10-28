import type { Page } from '@playwright/test';
import { expect, test } from './fixtures';
import { createLeague, teamName, type League } from './league';

// The season page's dialogs (Create Bracket, Set Seeds, Create Regular Season Matches): submitting
// closes the dialog and refreshes the page, and so does pressing Esc right after submitting, which
// closes the dialog before the request has finished.

const isCall = (path: RegExp) => (r: { url(): string; request(): { method(): string } }) => path.test(r.url());
const bracketLink = (page: Page, name: string) => page.locator('a', { hasText: name }); // <a> without href
const dialog = (page: Page) => page.locator('mat-dialog-container');

async function createBracket(page: Page, thenEscape: boolean): Promise<void> {
  await bracketLink(page, 'Create Bracket').click();
  await dialog(page).locator('input[name="participantsCount"]').fill('4');
  await dialog(page).locator('input[name="bestOf"]').fill('1');
  const created = page.waitForResponse(isCall(/\/api\/Brackets\/CreateBracket/i));
  const refreshed = page.waitForResponse(isCall(/\/api\/Brackets\/GetViewerData/i));
  await dialog(page).getByRole('button', { name: 'Create Bracket' }).click();
  if (thenEscape) await page.keyboard.press('Escape');
  await expect(dialog(page)).toHaveCount(0);
  expect((await created).ok()).toBe(true);
  expect((await refreshed).ok()).toBe(true);
}

test.describe('season page dialogs', () => {
  let league: League;

  test.beforeAll(async ({ baseUrl, admin }) => {
    league = await createLeague(baseUrl, admin, 'dialogs', { players: 4, captains: 4 });
  });
  test.afterAll(async () => league?.close());

  test('Create Bracket: Esc right after submit still refreshes the bracket', async ({ adminPage: page }) => {
    await page.goto(`/season/${league.seasonID}`);
    await expect(page.getByText('No bracket created')).toBeVisible();

    await createBracket(page, true);

    await expect(page.getByText('No bracket created')).toHaveCount(0);
    await expect(page.locator('.brackets-viewer')).toContainText('Seed 1');
  });

  test('Set Seeds: submit closes and refreshes; Esc right after a second submit still refreshes', async ({ adminPage: page, admin }) => {
    const viewer = page.locator('.brackets-viewer');
    await admin.get(`Brackets/CreateBracket?seasonID=${league.seasonID}&participantsCount=4&doubleElimination=false&bestOf=1`);
    await page.goto(`/season/${league.seasonID}`);
    await expect(viewer).toContainText('Seed 1');

    // Normal submit.
    await bracketLink(page, 'Set Seeds').click();
    await expect(dialog(page).locator('.seed-item:not(.dndPlaceholder)')).toHaveCount(4);
    let seeded = page.waitForResponse(isCall(/\/api\/Brackets\/SetCustomSeeds/i));
    let refreshed = page.waitForResponse(isCall(/\/api\/Brackets\/GetViewerData/i));
    await dialog(page).getByRole('button', { name: 'Set Seeds' }).click();
    expect((await seeded).ok()).toBe(true);
    await expect(dialog(page)).toHaveCount(0);
    expect((await refreshed).ok()).toBe(true);
    await expect(viewer).toContainText(teamName(league, 0));

    // A seeded bracket can't be seeded again (the server refuses): start a fresh one first.
    await admin.get(`Brackets/CreateBracket?seasonID=${league.seasonID}&participantsCount=4&doubleElimination=false&bestOf=1`);
    await page.reload();
    await expect(viewer).toContainText('Seed 1');
    await expect(viewer).not.toContainText(teamName(league, 0));

    // Submit, then Esc at once.
    await bracketLink(page, 'Set Seeds').click();
    await expect(dialog(page).locator('.seed-item:not(.dndPlaceholder)')).toHaveCount(4);
    seeded = page.waitForResponse(isCall(/\/api\/Brackets\/SetCustomSeeds/i));
    refreshed = page.waitForResponse(isCall(/\/api\/Brackets\/GetViewerData/i));
    await dialog(page).getByRole('button', { name: 'Set Seeds' }).click();
    await page.keyboard.press('Escape');
    await expect(dialog(page)).toHaveCount(0);
    expect((await seeded).ok()).toBe(true);
    expect((await refreshed).ok()).toBe(true);
    await expect(viewer).toContainText(teamName(league, 0));
  });

  test('Create Regular Season Matches: submit closes and refreshes; Esc right after submit refreshes once', async ({ adminPage: page }) => {
    const unscheduled = page.locator('mat-card', { has: page.getByText('Unscheduled Matches') });
    // Four teams, one home match against each other team: 12 matches.
    const matches = unscheduled.locator('mat-card-content > div');

    await page.goto(`/season/${league.seasonID}`);
    await expect(unscheduled).toBeVisible();
    await expect(matches).toHaveCount(0);

    for (const thenEscape of [false, true]) {
      await page.getByRole('button', { name: 'Create Regular Season Matches' }).click();
      await dialog(page).locator('input[name="homeMatchesPerTeam"]').fill('1');
      await dialog(page).locator('input[name="bestOf"]').fill('1');

      let refreshes = 0;
      const count = (r: { url(): string }) => { if (/\/api\/MatchPlanner\/GetUnscheduledMatches/i.test(r.url())) refreshes++; };
      page.on('response', count);
      const created = page.waitForResponse(isCall(/\/api\/MatchPlanner\/CreateSeasonMatches/i));
      await dialog(page).getByRole('button', { name: 'Create Regular Matches' }).click();
      if (thenEscape) await page.keyboard.press('Escape');
      await expect(dialog(page)).toHaveCount(0);
      expect((await created).ok()).toBe(true);

      await expect(matches).toHaveCount(12);
      // The refresh after creating happens once, not again when the dialog closes.
      await expect.poll(() => refreshes).toBe(1);
      await page.waitForTimeout(1_000);
      expect(refreshes, thenEscape ? 'refreshes after submit + Esc' : 'refreshes after submit').toBe(1);
      page.off('response', count);
    }
  });
});
