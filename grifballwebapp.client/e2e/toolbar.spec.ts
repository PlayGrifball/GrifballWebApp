import { expect, test, type Page } from '@playwright/test';
import { META } from './mockApi.mts';
import { mockBackend, settled, SITE } from './testPages.mts';

// #307: signing in (on the login page, not with a page load) swaps the header's Login button for the wider user
// chip and adds the organizer and admin links. The bar must be laid out again: every link in it whole and clear of
// the chip, the rest behind the menu button. Screenshotted to screenshots/toolbar-signed-in/, which CI puts on a
// contact sheet on the PR (e2e/contact-sheets.mjs).

const SIZES = [
  ['desktop', 1920, 1080],
  ['laptop', 1280, 720],
  ['tablet-landscape', 1024, 768],
  ['tablet-portrait', 768, 1024],
  ['phone-portrait', 390, 844],
] as const;

/** Mocks the backend for a visitor whose password sign-in makes them an admin (the most links). */
async function mockSignIn(page: Page): Promise<void> {
  await mockBackend(page, 'anon');
  let signedIn = false;
  // Registered last, so tried first: signing in and out, and who is signed in.
  await page.route(url => /^\/api\/identity\/(login|logout|me)$/i.test(url.pathname), route => {
    if (route.request().method() === 'POST') {
      signedIn = /login$/i.test(new URL(route.request().url()).pathname);
      return route.fulfill({ status: 200, body: '' });
    }
    return signedIn
      ? route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(META.admin) })
      : route.fulfill({ status: 204, body: '' });
  });
}

/** What is wrong with the header's layout: links cut off or under the tools, or links with no way to them. */
function headerProblems(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const problems: string[] = [];
    const visible = (el: Element) => getComputedStyle(el).visibility !== 'hidden' && el.getBoundingClientRect().width > 0;
    const bar = document.querySelector('.toolbar-links')!.getBoundingClientRect();
    const tools = document.querySelector('.hud-header__tools')!.getBoundingClientRect();
    const links = [...document.querySelectorAll('.toolbar-links a.nav-link:not(#hidden)')].filter(visible);
    for (const link of links) {
      const r = link.getBoundingClientRect();
      const name = (link.textContent ?? '').trim();
      if (r.left < bar.left - 1 || r.right > bar.right + 1)
        problems.push(`"${name}" is cut off (${Math.round(r.left)} to ${Math.round(r.right)}, the bar ${Math.round(bar.left)} to ${Math.round(bar.right)})`);
      if (r.right > tools.left + 1)
        problems.push(`"${name}" runs under the user chip`);
    }
    if (document.documentElement.scrollWidth > innerWidth + 1)
      problems.push('the page scrolls sideways');
    const all = document.querySelectorAll('mat-sidenav a').length;
    const menu = document.querySelector('.hud-header__menu');
    if (links.length < all && !(menu && visible(menu)))
      problems.push(`${all - links.length} links do not fit and there is no menu button`);
    return problems;
  });
}

test('the toolbar lays out its links again on signing in, at every screen size', async ({ page }) => {
  test.setTimeout(90_000);
  await mockSignIn(page);
  const problems: string[] = [];
  for (const [index, [device, width, height]] of SIZES.entries()) {
    await page.setViewportSize({ width, height });
    await page.goto(SITE + '/login', { waitUntil: 'networkidle' });
    // Signed out, as the page was rendered: the Login button
    await expect(page.locator('.hud-header__login')).toBeVisible();
    await page.evaluate(() => document.fonts.ready);

    await page.locator('input[name="username"]').fill('grif');
    await page.locator('input[name="password"]').fill('pw');
    await page.getByRole('button', { name: 'Submit' }).click();
    await expect(page.locator('.hud-user')).toBeVisible();
    await expect(page).toHaveURL(/\/$/);
    // The admin links are in, in the menu at least
    await expect(page.locator('mat-sidenav a', { hasText: 'Database Backup' })).toHaveCount(1);
    await settled(page);
    await page.screenshot({ path: `screenshots/toolbar-signed-in/${index + 1}-${device}-${width}x${height}.png` });

    for (const p of await headerProblems(page))
      problems.push(`${width}x${height}: ${p}`);

    // The links that do not fit are a keypress away: the menu button opens them, focusable, and closes again.
    const menu = page.locator('.hud-header__menu');
    if (await menu.isVisible()) {
      await menu.focus();
      await page.keyboard.press('Enter');
      await expect(page.locator('mat-sidenav a', { hasText: 'Database Backup' })).toBeInViewport();
      await page.getByRole('button', { name: 'Close menu' }).click();
      await expect(page.locator('mat-sidenav')).toBeHidden();
    }

    // Signed out again for the next size
    await page.locator('button.hud-user').click();
    await page.getByRole('menuitem', { name: 'Log out' }).click();
    await expect(page.locator('.hud-header__login')).toBeVisible();
    // ...and the links have the room back: none cut off beside the Login button
    for (const p of await headerProblems(page))
      problems.push(`${width}x${height}, signed out again: ${p}`);
  }
  expect(problems).toEqual([]);
});
