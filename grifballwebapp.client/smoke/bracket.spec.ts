import type { Page } from '@playwright/test';
import { expect, test } from './fixtures';
import { createLeague, teamName, type League } from './league';

// The playoff bracket on the season page: making one, how an unseeded and a seeded bracket show,
// and a season that doesn't exist.

interface ViewerData {
  matches: { id: number; status: unknown; opponent1: { id: number | null } | null; opponent2: { id: number | null } | null }[];
}

const viewerData = (page: Page) => page.waitForResponse(r => /\/api\/Brackets\/GetViewerData/i.test(r.url()) && r.request().method() === 'GET');

test.describe('playoff bracket', () => {
  let league: League;

  test.beforeAll(async ({ baseUrl, admin }) => {
    league = await createLeague(baseUrl, admin, 'bracket', { players: 4, captains: 4 });
  });
  test.afterAll(async () => league?.close());

  test('a season that does not exist shows "No bracket created", with no error', async ({ adminPage: page, consoleErrors }) => {
    // Far above any real season ID: the app's own 404s for it are expected.
    const missing = 2_000_000_000;
    consoleErrors.allow(new RegExp(`status of 404.*${missing}`));

    const viewer = viewerData(page);
    await page.goto(`/season/${missing}?tab=bracket`);
    expect((await viewer).status()).toBe(404);
    await expect(page.getByText('No bracket created')).toBeVisible();
    await expect(page.locator('mat-snack-bar-container')).toHaveCount(0);
  });

  test('creating a bracket shows unseeded "Seed N" slots, locked; seeding fills in the teams', async ({ adminPage: page, admin }) => {
    await page.goto(`/season/${league.seasonID}?tab=bracket`);
    await expect(page.getByText('No bracket created')).toBeVisible();

    await page.getByRole('button', { name: 'Create Bracket' }).click();
    const dialog = page.locator('mat-dialog-container');
    await dialog.locator('input[name="participantsCount"]').fill('4');
    await dialog.getByRole('checkbox', { name: 'Double Elimination' }).uncheck();
    await dialog.locator('input[name="bestOf"]').fill('1');
    const refreshed = viewerData(page);
    await dialog.getByRole('button', { name: 'Create Bracket' }).click();

    // The dialog closes and the page shows the new bracket.
    await expect(dialog).toHaveCount(0);
    const unseeded = await (await refreshed).json() as ViewerData;
    await expect(page.getByText('No bracket created')).toHaveCount(0);
    const viewer = page.locator('.brackets-viewer');
    await expect(viewer).toContainText('Seed 1');
    await expect(viewer).toContainText('Seed 4');
    // brackets-viewer marks a match Locked (Status.Locked = 0) with data-match-status, not text.
    await expect(viewer.locator('[data-match-status="0"]').first()).toBeAttached();

    // Statuses are numbers (brackets-model's Status enum), not names.
    expect(unseeded.matches).toHaveLength(3);
    for (const match of unseeded.matches) {
      expect(typeof match.status).toBe('number');
    }

    // Seeding by standings puts the teams in the first round.
    await admin.post(`Brackets/SetSeeds/${league.seasonID}`);
    // The reloaded page is drawn on the server, which fetches the bracket itself (the browser then
    // makes no request for it): the data straight from the API, then the page.
    const data = await admin.get<ViewerData>(`Brackets/GetViewerData?seasonID=${league.seasonID}`);
    await page.reload();
    for (const match of data.matches) {
      expect(typeof match.status).toBe('number');
    }
    const firstRound = data.matches.filter(m => m.opponent1?.id != null && m.opponent2?.id != null);
    expect(firstRound).toHaveLength(2);
    for (let i = 0; i < 4; i++) {
      await expect(viewer).toContainText(teamName(league, i));
    }
  });
});
