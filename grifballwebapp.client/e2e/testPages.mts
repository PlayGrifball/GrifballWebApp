// The site's routes and its dialogs/menus/pickers, shared by e2e/a11y.spec.ts (axe on every page and
// every overlay) and e2e/design-rules.spec.ts (font size, padding, tap targets, clipping, overflow
// and overlap on every page and every overlay). Keeping the list and the "how do I open this
// overlay" steps in one place means both specs check the same pages in the same states.
import { expect, type Locator, type Page } from '@playwright/test';
import { MOCK_NOW, mockResponse, signIn, type Role } from './mockApi.mts';

export const SITE = `http://localhost:${process.env.E2E_PORT ?? 4300}`;

// The pages anyone can open. Signed in, also the user's own profile; a visitor also has the pages
// for signing in, which a signed-in user has no use for.
export const PUBLIC = [
  '/', '/theme', '/season/12', '/season/12?tab=schedule', '/season/12?tab=bracket', '/season/12/signups',
  '/season/13/signupForm', '/season/13/teams', '/season/12/team/1', '/season/12/playergrades', '/seasonmatch/31',
  '/reschedule-request/31', '/no-such-page',
  // A season backfilled from the historical archive: its banner, champions, stats and what-is-missing notices
  '/season/5', '/season/5?tab=schedule', '/season/5/team/41',
];
export const VISITOR = ['/profile/117', '/login', '/register', '/reset-password?token=e2e-reset-token'];
// The commissioners' and admins' pages (a visitor is sent home from them)
export const SIGNED_IN = [
  '/profile/999', '/seasonManager', '/seasonEdit/0', '/seasonEdit/12', '/seasonAvailability/12', '/commissioner-dashboard',
  '/excel', '/excel/1xGrifballAutumnBrawl2026/Week%203', '/infiniteclient', '/usermanagement', '/usermanagement/createuser',
  '/usermanagement/edituser/2', '/usermanagement/mergeuser', '/usermanagement/mergeuser/2', '/usermanagement/mergeuser/2/3',
];

/** Every route, by role: a visitor's pages, and a signed-in commissioner/admin's. */
export const ROUTES: { role: Role, label: string, paths: string[] }[] = [
  { role: 'anon', label: 'signed out', paths: [...PUBLIC, ...VISITOR] },
  { role: 'admin', label: 'signed in', paths: [...PUBLIC, ...SIGNED_IN] },
];

/** Logs in as `role` (nothing for a visitor), freezes the clock and mocks the backend. */
export async function mockBackend(page: Page, role: Role): Promise<void> {
  await signIn(page.context(), role);
  await page.clock.setFixedTime(new Date(MOCK_NOW));
  // Hermetic: the app and its fonts are all served locally.
  await page.route(url => url.hostname !== 'localhost', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));
  await page.route('**/api/**', route => {
    const mocked = mockResponse(role, new URL(route.request().url()).pathname);
    return mocked ? route.fulfill(mocked) : route.fulfill({ status: 404, body: '' });
  });
}

/** Opens a page and waits for it to settle: its data loaded, the web fonts in, rows faded in. */
export async function open(page: Page, path: string): Promise<void> {
  await page.goto(SITE + path, { waitUntil: 'networkidle' });
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(500);
}

/**
 * A dialog, menu or picker to check in its open state: `role` and `path` are what to mock and open
 * first, `setup` opens the overlay (clicking, hovering, ...) and returns the locator that is now
 * open, or null if this overlay does not apply at the current viewport (the nav drawer's button only
 * shows on narrow screens). `close`: undefined closes with Escape (and checks the overlay is gone);
 * a function replaces that with its own close-and-check; null (a tooltip) does neither, since the
 * next navigation clears it and hovering away is not worth the flake.
 */
export interface Overlay {
  name: string;
  role: Role;
  path: string;
  setup(page: Page): Promise<Locator | null>;
  close?: ((page: Page) => Promise<void>) | null;
}

async function openThemeDialog(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'Theme icon' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.locator('app-tile').first()).toContainText('#');
  return dialog;
}

export const OVERLAYS: Overlay[] = [
  {
    name: 'theme dialog', role: 'anon', path: '/',
    setup: openThemeDialog,
  },
  {
    name: 'theme dialog, font list', role: 'anon', path: '/',
    setup: async page => {
      const dialog = await openThemeDialog(page);
      await dialog.getByRole('combobox').click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toBeVisible();
      return listbox;
    },
  },
  {
    name: 'theme dialog, colour picker', role: 'anon', path: '/',
    setup: async page => {
      const dialog = await openThemeDialog(page);
      await dialog.getByRole('button', { name: 'Pick the primary colour' }).click();
      const picker = page.locator('mtx-colorpicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'user menu', role: 'admin', path: '/',
    setup: async page => {
      await page.locator('button.hud-user').click();
      const menu = page.getByRole('menu');
      await expect(menu).toBeVisible();
      return menu;
    },
  },
  {
    name: 'navigation drawer', role: 'admin', path: '/',
    // The menu button is there when the links do not all fit in the header (on phones); at wider
    // sizes there is nothing to open.
    setup: async page => {
      const menuButton = page.locator('.hud-header__menu');
      if (!(await menuButton.isVisible()))
        return null;
      await menuButton.click();
      const drawer = page.locator('mat-sidenav');
      await expect(page.locator('mat-sidenav a').first()).toBeInViewport();
      return drawer;
    },
    close: async page => {
      await page.getByRole('button', { name: 'Close menu' }).click();
      await expect(page.locator('mat-sidenav')).toBeHidden();
    },
  },
  {
    name: 'navigation drawer, signed out', role: 'anon', path: '/',
    setup: async page => {
      const menuButton = page.locator('.hud-header__menu');
      if (!(await menuButton.isVisible()))
        return null;
      await menuButton.click();
      const drawer = page.locator('mat-sidenav');
      await expect(page.locator('mat-sidenav a').first()).toBeInViewport();
      return drawer;
    },
    close: async page => {
      await page.getByRole('button', { name: 'Close menu' }).click();
      await expect(page.locator('mat-sidenav')).toBeHidden();
    },
  },
  {
    name: 'create matches dialog', role: 'admin', path: '/season/12?tab=schedule',
    setup: async page => {
      await page.getByRole('button', { name: 'Create Regular Season Matches' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      return dialog;
    },
  },
  {
    name: 'schedule, match time picker', role: 'admin', path: '/season/12?tab=schedule',
    setup: async page => {
      await page.getByRole('button', { name: 'Show Edit' }).click();
      await page.locator('.sched-match__form mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'create bracket dialog', role: 'admin', path: '/season/12?tab=bracket',
    setup: async page => {
      await page.getByRole('button', { name: 'Create Bracket' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      return dialog;
    },
  },
  {
    name: 'seeds dialog', role: 'admin', path: '/season/12?tab=bracket',
    setup: async page => {
      await page.getByRole('button', { name: 'Set Seeds' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toContainText('Red Rampage');
      return dialog;
    },
  },
  {
    name: 'process reschedule dialog', role: 'admin', path: '/commissioner-dashboard',
    setup: async page => {
      await page.getByRole('button', { name: 'Process' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      return dialog;
    },
  },
  {
    name: 'Discord thread tooltip', role: 'admin', path: '/commissioner-dashboard',
    setup: async page => {
      await page.getByRole('button', { name: 'Create Discord thread' }).hover();
      const tooltip = page.locator('.mat-mdc-tooltip');
      await expect(tooltip).toBeVisible();
      return tooltip;
    },
    close: null, // a tooltip needs no Esc; the next navigation clears it
  },
  {
    name: 'user management tooltip', role: 'admin', path: '/usermanagement',
    setup: async page => {
      await page.getByRole('link', { name: 'EDIT', exact: true }).first().hover();
      const tooltip = page.locator('.mat-mdc-tooltip');
      await expect(tooltip).toBeVisible();
      return tooltip;
    },
    close: null,
  },
  {
    name: 'edit user, lockout picker', role: 'admin', path: '/usermanagement/edituser/2',
    setup: async page => {
      await page.locator('mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'season editor, date picker', role: 'admin', path: '/seasonEdit/12',
    setup: async page => {
      await page.locator('mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'season editor, copy-from list', role: 'admin', path: '/seasonEdit/12',
    setup: async page => {
      await page.getByRole('combobox', { name: 'Copy from' }).click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toContainText('Winter Campaign');
      return listbox;
    },
  },
  {
    name: 'availability, time picker', role: 'admin', path: '/seasonAvailability/12',
    setup: async page => {
      await page.locator('mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'availability, day list', role: 'admin', path: '/seasonAvailability/12',
    setup: async page => {
      await page.getByRole('combobox').first().click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toBeVisible();
      return listbox;
    },
  },
  {
    name: 'reschedule date picker', role: 'admin', path: '/reschedule-request/31',
    setup: async page => {
      await page.locator('mat-datepicker-toggle button').click();
      const picker = page.locator('mat-datepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
];

/**
 * Closes an overlay opened by `Overlay.setup` and (except for a tooltip, `close: null`) checks it is
 * gone: Esc by default, or the overlay's own `close`.
 */
export async function closeOverlay(page: Page, overlay: Overlay, opened: Locator): Promise<void> {
  if (overlay.close === null)
    return;
  if (overlay.close) {
    // Its own close already checks the overlay is gone (the nav drawer stays in the DOM, just
    // hidden, so toHaveCount(0) below would never pass for it).
    await overlay.close(page);
    return;
  }
  await page.keyboard.press('Escape');
  await expect(opened).toHaveCount(0);
}
