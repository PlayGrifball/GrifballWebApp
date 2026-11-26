import { expect, test } from '@playwright/test';
import { mockBackend, open } from './testPages.mts';

// #9 of the review on PR #624: on a phone, two single-class CSS rules (.flipyap--start .flipyap__chat,
// .flipyap--below .flipyap__chat) outranked the one-class @media (max-width: 560px) rule that pins the chat along
// the bottom as a sheet, so dragging him to the top-left left the sheet pinned near the top instead.
test('Flipyap\'s chat is a bottom sheet on a phone, even dragged to the top left where it would otherwise open there', async ({ page }) => {
  await mockBackend(page, 'anon');
  await page.setViewportSize({ width: 360, height: 740 });
  await open(page, '/');

  const pet = page.getByRole('button', { name: 'Chat with Flipyap' });
  const start = (await pet.boundingBox())!;
  await page.mouse.move(start.x + start.width / 2, start.y + start.height / 2);
  await page.mouse.down();
  await page.mouse.move(10, 10, { steps: 5 });
  await page.mouse.up();

  await pet.click();
  const panel = page.locator('.flipyap-chat');
  await expect(panel).toBeVisible();
  const rect = (await panel.boundingBox())!;
  const viewport = page.viewportSize()!;

  // The sheet's own gutters (flipyap.component.scss: 16px either side, 88px off the bottom), not his dragged corner
  expect(rect.x).toBeGreaterThanOrEqual(14);
  expect(rect.x + rect.width).toBeLessThanOrEqual(viewport.width - 14);
  expect(rect.y + rect.height).toBeGreaterThan(viewport.height / 2);
  expect(rect.y + rect.height).toBeLessThanOrEqual(viewport.height - 10);
});
