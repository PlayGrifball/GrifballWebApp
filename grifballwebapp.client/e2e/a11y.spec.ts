import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { MOCK_NOW, mockResponse, signIn, type Role } from './mockApi.mts';

// Accessibility, checked by axe-core (https://github.com/dequelabs/axe-core) against WCAG 2.x A and
// AA: every page at a phone and a desktop size, as a visitor and signed in as a commissioner and
// admin ("Commander Grif" in e2e/mockApi.mts, as Lighthouse signs in: lighthouserc.cjs), and every
// dialog, menu and picker in its open state, which Lighthouse never sees. A serious or critical
// violation fails the test; the summary lists each one by size, page, rule and element.
//
// The pages are rendered on the server (localhost, e2e/serve.mjs) and taken over by the browser,
// with the API mocked here from e2e/mockApi.mts, as in the layout test (layout.spec.ts).

const SITE = `http://localhost:${process.env.E2E_PORT ?? 4300}`;

const SIZES = [
  ['phone', 360, 640],
  ['desktop', 1280, 720],
] as const;

const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

// The pages anyone can open. Signed in, also the user's own profile; a visitor also has the pages
// for signing in, which a signed-in user has no use for.
const PUBLIC = [
  '/', '/theme', '/season/12', '/season/12?tab=schedule', '/season/12?tab=bracket', '/season/12/signups',
  '/season/13/signupForm', '/season/13/teams', '/season/12/team/1', '/season/12/playergrades', '/seasonmatch/31',
  '/topstats', '/lateLeague', '/reschedule-request/31', '/no-such-page',
];
const VISITOR = ['/profile/117', '/login', '/register', '/reset-password?token=e2e-reset-token'];
// The commissioners' and admins' pages (a visitor is sent home from them)
const SIGNED_IN = [
  '/profile/999', '/seasonManager', '/seasonEdit/0', '/seasonEdit/12', '/seasonAvailability/12', '/commissioner-dashboard',
  '/excel', '/excel/1xGrifballAutumnBrawl2026/Week%203', '/infiniteclient', '/usermanagement', '/usermanagement/createuser',
  '/usermanagement/edituser/2', '/usermanagement/mergeuser', '/usermanagement/mergeuser/2', '/usermanagement/mergeuser/2/3',
];

/** Logs in as `role` (nothing for a visitor), freezes the clock and mocks the backend. */
async function mockBackend(page: Page, role: Role): Promise<void> {
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
async function open(page: Page, path: string): Promise<void> {
  await page.goto(SITE + path, { waitUntil: 'networkidle' });
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(500);
}

// Third-party markup the app does not write: the date and time pickers (Angular Material's and
// @ng-matero/extensions') and the colour picker (@acrodata/color-picker, in @ng-matero/extensions).
// Each is checked like the rest of the page (colours and sizes are the app's theme) but for these
// rules, and there only.
const THIRD_PARTY: { within: string, rules: string[] }[] = [
  { within: 'mat-datepicker-content, mtx-datetimepicker-content', rules: [
    // The header's buttons show the date ("2026", "Thu, Oct 29", "18", "00") and are named for
    // what they do ("Choose month and year", "Choose hour", ...)
    'label-content-name-mismatch',
    // mtx-calendar puts its role="grid" days in a role="table"
    'aria-required-children',
    // mtx-clock has role="clock", which is not an ARIA role
    'aria-roles',
  ] },
  // The saturation, hue and alpha thumbs, the preview and the format toggles are unnamed buttons
  { within: 'mtx-colorpicker-content', rules: ['button-name'] },
  // While the colour picker is open, its input's aria-owns names the picker's id, which its
  // overlay does not carry (the input is checked with the picker closed)
  { within: 'app-palette input[aria-owns^="mtx-colorpicker-"]', rules: ['aria-valid-attr-value'] },
];

/** The page's serious and critical violations as it is now, one line per element. */
async function scan(page: Page, where: string): Promise<string[]> {
  // Animations done: an overlay fading in is measured at its final colours
  await page.waitForTimeout(300);
  let axe = new AxeBuilder({ page }).withTags(TAGS);
  for (const { within } of THIRD_PARTY)
    axe = axe.exclude(within);
  const { violations } = await axe.analyze();
  for (const { within, rules } of THIRD_PARTY) {
    if (await page.locator(within).count())
      violations.push(...(await new AxeBuilder({ page }).withTags(TAGS).include(within).disableRules(rules).analyze()).violations);
  }
  return violations
    .filter(v => v.impact === 'serious' || v.impact === 'critical')
    .flatMap(v => v.nodes.map(node => {
      const why = (node.failureSummary ?? '').split('\n').slice(1).map(l => l.trim()).filter(Boolean)[0] ?? '';
      return `${where}: ${v.id} (${v.impact}) ${v.help}: ${node.target.join(' ')}${why ? ` (${why})` : ''}`;
    }));
}

/** Clicks `opener`, waits for `overlay`, scans the page with it open, then closes it with Esc. */
async function scanOpen(page: Page, where: string, opener: Locator, overlay: Locator): Promise<string[]> {
  await opener.click();
  await expect(overlay.first()).toBeVisible();
  const found = await scan(page, where);
  await page.keyboard.press('Escape');
  await expect(overlay).toHaveCount(0);
  return found;
}

for (const [role, label, paths] of [['anon', 'signed out', [...PUBLIC, ...VISITOR]], ['admin', 'signed in', [...PUBLIC, ...SIGNED_IN]]] as const) {
  test.describe(label, () => {
    for (const path of paths) {
      test(`${path} has no serious accessibility problems`, async ({ page }) => {
        await mockBackend(page, role);
        const problems: string[] = [];
        for (const [device, width, height] of SIZES) {
          await page.setViewportSize({ width, height });
          await open(page, path);
          problems.push(...await scan(page, `${device} ${path}`));
        }
        expect(problems).toEqual([]);
      });
    }
  });
}

// The overlays, each scanned while open. The page behind is scanned too (the overlay hides it from
// assistive technology while it is open, which axe takes into account).
test.describe('open', () => {
  test('the theme dialog and its font list', async ({ page }) => {
    await mockBackend(page, 'anon');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/');
      await page.getByRole('button', { name: 'Theme icon' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog.locator('app-tile').first()).toContainText('#');
      problems.push(...await scan(page, `${device} theme dialog`));
      problems.push(...await scanOpen(page, `${device} theme dialog, font list`, dialog.getByRole('combobox'), page.getByRole('listbox')));
      problems.push(...await scanOpen(page, `${device} theme dialog, colour picker`,
        dialog.getByRole('button', { name: 'Pick the primary colour' }), page.locator('mtx-colorpicker-content')));
      await page.keyboard.press('Escape');
      await expect(dialog).toHaveCount(0);
    }
    expect(problems).toEqual([]);
  });

  test('the user menu and the navigation drawer', async ({ page }) => {
    await mockBackend(page, 'admin');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/');
      problems.push(...await scanOpen(page, `${device} user menu`, page.locator('button.hud-user'), page.getByRole('menu')));
      // The menu button is there when the links do not all fit in the header (on phones)
      const menu = page.locator('.hud-header__menu');
      if (await menu.isVisible()) {
        await menu.click();
        await expect(page.locator('mat-sidenav a').first()).toBeInViewport();
        problems.push(...await scan(page, `${device} navigation drawer`));
        await page.getByRole('button', { name: 'Close menu' }).click();
        await expect(page.locator('mat-sidenav')).toBeHidden();
      }
    }
    // A visitor's drawer, on a phone
    await page.context().clearCookies();
    await page.setViewportSize({ width: 360, height: 640 });
    await open(page, '/');
    await page.locator('.hud-header__menu').click();
    await expect(page.locator('mat-sidenav a').first()).toBeInViewport();
    expect([...problems, ...await scan(page, 'phone navigation drawer, signed out')]).toEqual([]);
  });

  test('the schedule\'s dialog and time pickers', async ({ page }) => {
    await mockBackend(page, 'admin');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/season/12?tab=schedule');
      problems.push(...await scanOpen(page, `${device} create matches dialog`,
        page.getByRole('button', { name: 'Create Regular Season Matches' }), page.getByRole('dialog')));
      await page.getByRole('button', { name: 'Show Edit' }).click();
      problems.push(...await scan(page, `${device} schedule, editing`));
      problems.push(...await scanOpen(page, `${device} schedule, match time picker`,
        page.locator('.sched-match__form mtx-datetimepicker-toggle button').first(), page.locator('mtx-datetimepicker-content')));
    }
    expect(problems).toEqual([]);
  });

  test('the bracket\'s dialogs', async ({ page }) => {
    await mockBackend(page, 'admin');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/season/12?tab=bracket');
      problems.push(...await scanOpen(page, `${device} create bracket dialog`, page.getByRole('button', { name: 'Create Bracket' }), page.getByRole('dialog')));
      await page.getByRole('button', { name: 'Set Seeds' }).click();
      await expect(page.getByRole('dialog')).toContainText('Red Rampage');
      problems.push(...await scan(page, `${device} seeds dialog`));
      await page.keyboard.press('Escape');
      await expect(page.getByRole('dialog')).toHaveCount(0);
    }
    expect(problems).toEqual([]);
  });

  test('the commissioner dashboard\'s dialog and tooltip', async ({ page }) => {
    await mockBackend(page, 'admin');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/commissioner-dashboard');
      problems.push(...await scanOpen(page, `${device} process reschedule dialog`, page.getByRole('button', { name: 'Process' }), page.getByRole('dialog')));
      await page.getByRole('button', { name: 'Create Discord thread' }).hover();
      await expect(page.locator('.mat-mdc-tooltip')).toBeVisible();
      problems.push(...await scan(page, `${device} Discord thread tooltip`));
    }
    expect(problems).toEqual([]);
  });

  test('user management\'s tooltips and the edit user lockout picker', async ({ page }) => {
    await mockBackend(page, 'admin');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/usermanagement');
      await page.getByRole('link', { name: 'EDIT', exact: true }).first().hover();
      await expect(page.locator('.mat-mdc-tooltip')).toBeVisible();
      problems.push(...await scan(page, `${device} user management tooltip`));
      await open(page, '/usermanagement/edituser/2');
      problems.push(...await scanOpen(page, `${device} edit user, lockout picker`,
        page.locator('mtx-datetimepicker-toggle button').first(), page.locator('mtx-datetimepicker-content')));
    }
    expect(problems).toEqual([]);
  });

  test('the season editor\'s date pickers, and the availability time picker and day list', async ({ page }) => {
    await mockBackend(page, 'admin');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/seasonEdit/12');
      problems.push(...await scanOpen(page, `${device} season editor, date picker`,
        page.locator('mtx-datetimepicker-toggle button').first(), page.locator('mtx-datetimepicker-content')));
      await open(page, '/seasonAvailability/12');
      problems.push(...await scanOpen(page, `${device} availability, time picker`,
        page.locator('mtx-datetimepicker-toggle button').first(), page.locator('mtx-datetimepicker-content')));
      problems.push(...await scanOpen(page, `${device} availability, day list`, page.getByRole('combobox').first(), page.getByRole('listbox')));
    }
    expect(problems).toEqual([]);
  });

  test('the reschedule request\'s date picker', async ({ page }) => {
    await mockBackend(page, 'admin');
    const problems: string[] = [];
    for (const [device, width, height] of SIZES) {
      await page.setViewportSize({ width, height });
      await open(page, '/reschedule-request/31');
      problems.push(...await scanOpen(page, `${device} reschedule date picker`,
        page.locator('mat-datepicker-toggle button'), page.locator('mat-datepicker-content')));
    }
    expect(problems).toEqual([]);
  });
});
