import { expect, test, type Page } from '@playwright/test';
import { type Role } from './mockApi.mts';
import { closeOverlay, mockBackend, open, OVERLAYS, SITE, slug } from './testPages.mts';

// Every key page at every screen size: no sideways scrolling, its main parts on screen and not on
// top of one another, the navigation reachable, and big enough touch targets on phones. Pages
// scroll down, so only the horizontal direction counts. The backend is mocked (e2e/mockApi.mts).
//
// Each page and size is screenshotted to screenshots/<page>/<n>-<device>-<width>x<height>.png, and each state a click
// reaches on it (a tab, a filter, a dialog: OVERLAYS in e2e/testPages.mts) to screenshots/<page>/<state>/...; CI turns
// them into one contact sheet per page (e2e/contact-sheets.mjs) and links them on the PR.
//
// The pages are rendered on the server, as on the site: they are opened on localhost, which the
// server renders for (e2e/serve.mjs). The browser then takes them over, with the API mocked here.

// The screen sizes, by device; the name goes in each screenshot's file name, which the contact
// sheets use for order and captions.
const SIZES = [
  ['desktop', 1920, 1080],
  ['laptop', 1280, 720],
  ['tablet-landscape', 1024, 768],
  ['tablet-portrait', 768, 1024],
  // A phone on its side with the browser's address and tab bars showing: little height is left
  ['phone-landscape', 820, 300],
  ['phone-portrait', 360, 640],
] as const;

const PHONES: readonly string[] = ['phone-landscape', 'phone-portrait'];

// The header's parts, on every page: they must fit side by side. "?" marks a part that may be
// left out (the menu button when every link fits, the links when none do).
const HEADER = ['?.hud-header__menu', '.hud-brand', '?.toolbar-links a.nav-link:not(#hidden)', '.hud-header__tools > *'];

interface PageCase {
  name: string;
  path: string;
  role: Role;
  /** Shown once the page's data has loaded (each of them, for a page with several parts loading on their own). */
  ready: string | string[];
  /** The parts that must each be on screen (horizontally) and must not overlap one another.
   * "text:" measures an element's text rather than its box, e.g. a heading in a wide box. */
  parts: string[];
  /** The contact sheet it goes on, as a section, when not its own: another variant of a page (signed out, as a
   * commissioner), or a page reached from it (a user management page). */
  sheet?: string;
}

const TABS = ['.hud-tab'];
// The Late League queue panel (on the home and Late League pages): loaded in the browser only, after the page
const QUEUE = '.late-league-queue__count';
const QUEUE_PARTS = [QUEUE, '.late-league-queue__actions'];

const PAGES: PageCase[] = [
  { name: 'home', path: '/', role: 'player', ready: ['tr.mat-mdc-row', QUEUE],
    parts: ['.home-hero .hud-kicker', 'text:.home-hero h1', '.home-hero .hud-lede', '.home-hero__actions a', 'main h2', 'a.hud-card', '.hud-table-wrap', ...QUEUE_PARTS] },
  { name: 'home-signed-out', path: '/', role: 'anon', ready: ['tr.mat-mdc-row', QUEUE],
    parts: ['text:.home-hero h1', '.home-hero__actions a', 'a.hud-card', ...QUEUE_PARTS] },
  { name: 'season', path: '/season/12', role: 'player', ready: '.week-card',
    parts: ['text:.season-head h1', ...TABS, 'main h2', 'app-team-standings', '.week-card', '.hud-more'] },
  { name: 'season-schedule', path: '/season/12?tab=schedule', role: 'player', ready: '.sched-match',
    parts: ['text:.season-head h1', ...TABS, '.sched-week'] },
  { name: 'season-bracket', path: '/season/12?tab=bracket', role: 'player', ready: '.brackets-viewer .match',
    parts: ['text:.season-head h1', ...TABS, '.bracket-box', '.bracket-legend > span'] },
  { name: 'match', path: '/seasonmatch/31', role: 'player', ready: '.lobby-possible',
    parts: ['text:.lobby-head h1', '.lobby-head .hud-actions > *', '.lobby-team__banner', '.lobby-player', '.lobby-score', '.lobby-info > section', '.lobby-possible'] },
  // A commissioner's: the scoreboard's tabs, Enter a game by hand and the Report Game tools
  { name: 'match-commissioner', path: '/seasonmatch/31', role: 'admin', ready: ['.lobby-possible', 'app-box-score'],
    parts: ['text:.lobby-head h1', ...TABS, 'app-box-score', 'app-manual-games .hud-actions > *', '.lobby-admin', '.lobby-possible'] },
  { name: 'profile', path: '/profile/117', role: 'player', ready: '.linked-login',
    parts: ['.profile-card', '.profile-main > section'] },
  { name: 'signup', path: '/season/13/signupForm', role: 'player', ready: '.enlist-step',
    parts: ['.enlist-form', '.enlist-timeline h2', '.enlist-step', '.enlist-timeline .hud-more'] },
  { name: 'login', path: '/login', role: 'anon', ready: '.login-panel',
    parts: ['.login-panel', '.login-side > *'] },
  { name: 'user-management', path: '/usermanagement', role: 'admin', ready: 'app-table tr.mat-mdc-row',
    parts: ['text:app-user-management h1', 'app-user-management .hud-page-head .hud-actions > a', 'app-search-box', 'app-table'] },
  { name: 'season-manager', path: '/seasonManager', role: 'admin', ready: 'tr.mat-mdc-row',
    parts: ['text:app-season-manager h1', 'app-season-manager .hud-page-head > a', 'app-season-manager .hud-table-wrap'] },
  { name: 'season-editor', path: '/seasonEdit/12', role: 'admin', ready: 'mat-select[name="copyFromSeasonID"]',
    parts: ['text:app-season-edit h1', 'app-season-edit section.hud-panel', 'app-season-edit .hud-actions > *'] },
  { name: 'commissioner-dashboard', path: '/commissioner-dashboard', role: 'admin', ready: '.cd-metric',
    parts: ['text:app-commissioner-dashboard h1', '.cd-metric', 'app-commissioner-dashboard .hud-table-wrap'] },
  // The season list and the all-time leaderboards
  { name: 'seasons', path: '/seasons', role: 'player', ready: '.season-card',
    parts: ['text:app-seasons h1', '.season-card'] },
  { name: 'leaderboards', path: '/leaderboards', role: 'player', ready: 'app-leaderboards table.board tbody tr',
    parts: ['text:app-leaderboards h1', ...TABS, 'app-leaderboards .hud-table-wrap'] },
  // A season's rosters, played in the app and backfilled from the archive
  { name: 'season-teams', path: '/season/12/teams', role: 'player', ready: '.team-card',
    parts: ['text:app-season-teams h1', '.teams-toolbar', '.team-card'] },
  { name: 'season-teams-archive', path: '/season/5/teams', role: 'player', ready: '.team-card',
    parts: ['text:app-season-teams h1', '.teams-toolbar', '.team-card'] },
  { name: 'draft-board', path: '/season/13/draft', role: 'player', ready: 'app-team-builder .player-pool-item',
    parts: ['text:app-team-builder h1'] },
  { name: 'team', path: '/season/12/team/1', role: 'player', ready: '.team-player',
    parts: ['text:app-team h1', 'text:.team-history-link', '.team-record', '.team-player'] },
  { name: 'team-history', path: '/history/team/1', role: 'player', ready: '.history-season',
    parts: ['text:app-team-history h1', '.history-total', '.history-season'] },
  { name: 'match-details', path: '/match/0f8fad5b-d9cb-469f-a165-70867728950e', role: 'player', ready: '.md-player',
    parts: ['text:app-match-details h1', '.md-score', '.md-player'] },
  { name: 'player-grades', path: '/season/12/playergrades', role: 'player', ready: 'app-player-grades tr.mat-mdc-row',
    parts: ['text:app-player-grades h1', 'app-player-grades .hud-table-wrap'] },
  // Late League: the Discord queue and the archive's seasons, and one of those seasons
  { name: 'late-league', path: '/late-league', role: 'player', ready: ['app-late-league table.board tbody tr', QUEUE],
    parts: ['text:app-late-league h1', 'app-late-league .hud-table-wrap', ...QUEUE_PARTS] },
  { name: 'late-league-season', path: '/late-league/3', role: 'player', ready: 'app-late-league table.board tbody tr',
    parts: ['text:app-late-league h1', 'app-late-league .facts', 'app-late-league .hud-table-wrap'] },
  // The other variants of the pages above: signed out, as a commissioner, from the archive, a match never pulled
  { name: 'profile-signed-out', sheet: 'profile', path: '/profile/117', role: 'anon', ready: '.profile-card',
    parts: ['.profile-card', '.profile-main > section'] },
  { name: 'profile-own-commissioner', sheet: 'profile', path: '/profile/999', role: 'admin', ready: '.linked-login',
    parts: ['.profile-card', '.profile-main > section'] },
  { name: 'season-schedule-commissioner', sheet: 'season-schedule', path: '/season/12?tab=schedule', role: 'admin', ready: '.sched-match',
    parts: ['text:.season-head h1', ...TABS, '.sched-week'] },
  { name: 'season-bracket-commissioner', sheet: 'season-bracket', path: '/season/12?tab=bracket', role: 'admin', ready: '.brackets-viewer .match',
    parts: ['text:.season-head h1', ...TABS, '.bracket-box'] },
  { name: 'draft-board-commissioner', sheet: 'draft-board', path: '/season/13/draft', role: 'admin', ready: 'app-team-builder .player-pool-item',
    parts: ['text:app-team-builder h1'] },
  { name: 'season-archive', path: '/season/5', role: 'player', ready: 'app-season-player-stats tbody tr',
    parts: ['text:.season-head h1', ...TABS, 'app-season-player-stats .hud-table-wrap'] },
  { name: 'season-archive-schedule', sheet: 'season-archive', path: '/season/5?tab=schedule', role: 'player', ready: 'app-data-notice',
    parts: ['text:.season-head h1', ...TABS, 'app-data-notice'] },
  { name: 'season-archive-bracket', sheet: 'season-archive', path: '/season/5?tab=bracket', role: 'player', ready: 'app-data-notice',
    parts: ['text:.season-head h1', ...TABS, 'app-data-notice'] },
  { name: 'team-archive', sheet: 'team', path: '/season/5/team/41', role: 'player', ready: '.team-player',
    parts: ['text:app-team h1', '.team-record', '.team-player'] },
  { name: 'match-details-not-found', sheet: 'match-details', path: '/match/00000000-0000-0000-0000-000000000000', role: 'player',
    ready: 'app-match-details .hud-empty', parts: ['text:app-match-details h1', 'app-match-details .hud-empty'] },
  // The pages not above: a season's signups, signing up and in, the theme, a missing page, asking for a reschedule
  { name: 'signups', path: '/season/12/signups', role: 'player', ready: 'app-signups tr.mat-mdc-row',
    parts: ['text:app-signups h1', 'app-signups .hud-table-wrap'] },
  { name: 'register', sheet: 'login', path: '/register', role: 'anon', ready: 'app-register form', parts: ['text:app-register h1', 'app-register form'] },
  { name: 'reset-password', sheet: 'login', path: '/reset-password?token=e2e-reset-token', role: 'anon', ready: 'app-password-reset form',
    parts: ['text:app-password-reset h1', 'app-password-reset form'] },
  { name: 'theme-page', sheet: 'theme-dialog', path: '/theme', role: 'anon', ready: 'app-theme app-tile', parts: ['app-theme .theme-head'] },
  { name: 'not-found', path: '/no-such-page', role: 'anon', ready: 'app-not-found h1', parts: ['text:app-not-found h1'] },
  { name: 'reschedule-request', path: '/reschedule-request/31', role: 'player', ready: 'app-reschedule-request form',
    parts: ['app-reschedule-request form'] },
  // The commissioners' and sysadmins' pages not above
  { name: 'season-editor-new', sheet: 'season-editor', path: '/seasonEdit/0', role: 'admin', ready: 'app-season-edit section.hud-panel',
    parts: ['text:app-season-edit h1', 'app-season-edit section.hud-panel'] },
  { name: 'season-availability', sheet: 'season-editor', path: '/seasonAvailability/12', role: 'admin', ready: 'app-season-availability mtx-datetimepicker-toggle',
    parts: ['text:app-season-availability h1'] },
  { name: 'excel', path: '/excel/1xGrifballAutumnBrawl2026/Week%203', role: 'admin', ready: 'app-excel form', parts: ['text:app-excel h1'] },
  { name: 'excel-blank', sheet: 'excel', path: '/excel', role: 'admin', ready: 'app-excel form', parts: ['text:app-excel h1'] },
  { name: 'infinite-client', path: '/infiniteclient', role: 'admin', ready: 'app-infinite-client h1', parts: ['text:app-infinite-client h1'] },
  { name: 'database-backup', path: '/databasebackup', role: 'admin', ready: 'app-database-backup mat-select', parts: ['text:app-database-backup h1'] },
  { name: 'gamertag-claims', path: '/usermanagement/claims', role: 'admin', ready: 'app-gamertag-claims tbody tr',
    parts: ['text:app-gamertag-claims h1', ...TABS, 'app-gamertag-claims .hud-table-wrap'] },
  { name: 'historical-import', path: '/usermanagement/historicalimport', role: 'admin', ready: 'app-historical-import tbody tr',
    parts: ['text:app-historical-import h1'] },
  { name: 'jobs', path: '/usermanagement/jobs', role: 'admin', ready: 'app-jobs tbody tr', parts: ['text:app-jobs h1', 'app-jobs .hud-table-wrap'] },
  { name: 'telemetry', path: '/usermanagement/telemetry', role: 'admin', ready: 'app-telemetry .hud-tab', parts: ['text:app-telemetry h1', ...TABS] },
  { name: 'telemetry-trace', sheet: 'telemetry', path: '/usermanagement/telemetry?trace=4bf92f3577b34da6a3ce929d0e0e4736', role: 'admin', ready: '.tm-span',
    parts: ['text:app-telemetry h1', ...TABS] },
  ...([
    ['create-user', '/usermanagement/createuser', 'app-create-user'], ['edit-user', '/usermanagement/edituser/2', 'app-edit-user'],
    ['merge-user', '/usermanagement/mergeuser', 'app-merge-user'], ['merge-user-from', '/usermanagement/mergeuser/2', 'app-merge-user'],
    ['merge-user-both', '/usermanagement/mergeuser/2/3', 'app-merge-user'], ['merges', '/usermanagement/merges', 'app-merges'],
    ['past-gamertags', '/usermanagement/pastgamertags', 'app-past-gamertags'],
    ['past-gamertags-account', '/usermanagement/pastgamertags?xboxUserID=2535400000000117', 'app-past-gamertags'],
  ] as const).map(([name, path, component]): PageCase => ({ name, sheet: 'user-management', path, role: 'admin', ready: `${component} h1`, parts: [`text:${component} h1`] })),
];

// What has to be at least 44x44 on a phone: the buttons and links people tap.
const TOUCH_TARGETS = [
  'header a:not(#hidden)', 'header button', '.hud-btn', '.hud-tab', '.hud-more', 'a.hud-card',
  'main .mat-mdc-button-base', '.home-archive__link', '.standings__team', '.sched-match__link', '.hud-cell-btn',
];

/** Every problem with the layout at the current size. */
function layoutProblems(page: Page, parts: string[], phone: boolean, touchTargets: string[]) {
  return page.evaluate(({ parts, phone, touchTargets }) => {
    const problems: string[] = [];
    const width = innerWidth;
    const visible = (el: Element) => {
      const r = el.getBoundingClientRect();
      return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden';
    };
    const name = (el: Element) => el.tagName.toLowerCase() + [...el.classList].slice(0, 2).map(c => '.' + c).join('');

    // Sideways scrolling, of the document or of the app's scrolling content area
    if (document.documentElement.scrollWidth > width + 1)
      problems.push(`the page scrolls sideways (${document.documentElement.scrollWidth}px wide)`);
    const content = document.querySelector('.mat-drawer-content');
    if (content && content.scrollWidth > content.clientWidth + 1)
      problems.push(`the content is ${content.scrollWidth}px wide`);

    // Anything sticking out of the screen that is not inside a box of its own that scrolls or clips
    const main = document.querySelector('main')!;
    for (const el of main.querySelectorAll('*')) {
      const r = el.getBoundingClientRect();
      if (r.width === 0 || (r.left >= -1 && r.right <= width + 1)) continue;
      let clipped = false;
      for (let p = el.parentElement; p && p !== main; p = p.parentElement) {
        if (/(auto|scroll|hidden|clip)/.test(getComputedStyle(p).overflowX)) { clipped = true; break; }
      }
      if (!clipped) problems.push(`${name(el)} sticks out of the screen (${Math.round(r.left)} to ${Math.round(r.right)})`);
    }

    // The main parts: on screen and apart
    const boxes: { name: string, r: DOMRect }[] = [];
    for (let entry of parts) {
      const optional = entry.startsWith('?');
      if (optional) entry = entry.slice(1);
      const text = entry.startsWith('text:');
      const selector = text ? entry.slice(5) : entry;
      const found = [...document.querySelectorAll(selector)].filter(visible);
      if (found.length === 0 && !optional) problems.push(`${selector} is missing`);
      found.forEach((el, i) => {
        let r = el.getBoundingClientRect();
        if (text) {
          const range = document.createRange();
          range.selectNodeContents(el);
          r = range.getBoundingClientRect();
        }
        boxes.push({ name: `${selector}[${i}]`, r });
      });
    }
    for (const { name, r } of boxes) {
      if (r.left < -1 || r.right > width + 1) problems.push(`${name} is off screen (${Math.round(r.left)} to ${Math.round(r.right)})`);
    }
    for (let i = 0; i < boxes.length; i++) {
      for (let j = i + 1; j < boxes.length; j++) {
        const a = boxes[i].r, b = boxes[j].r;
        const overlap = Math.min(a.right, b.right) - Math.max(a.left, b.left) > 1 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 1;
        if (overlap) problems.push(`${boxes[i].name} overlaps ${boxes[j].name}`);
      }
    }

    // The navigation: links that do not fit in the bar are behind a menu button that can be pressed
    const inBar = [...document.querySelectorAll('.toolbar-links a.nav-link:not(#hidden)')].filter(visible).length;
    const all = document.querySelectorAll('mat-sidenav a').length;
    const menu = document.querySelector('.hud-header__menu');
    if (inBar < all) {
      if (!menu || !visible(menu)) {
        problems.push(`${all - inBar} nav links do not fit and there is no menu button`);
      } else {
        const r = menu.getBoundingClientRect();
        const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
        if (r.left < 0 || r.right > width || r.top < 0 || r.bottom > innerHeight || !menu.contains(hit))
          problems.push('the menu button cannot be pressed');
      }
    }

    // Touch targets on phones
    if (phone) {
      for (const el of document.querySelectorAll(touchTargets.join(','))) {
        if (!visible(el)) continue;
        const r = el.getBoundingClientRect();
        if (r.width < 43.5 || r.height < 43.5)
          problems.push(`${name(el)} "${(el.textContent ?? '').trim().slice(0, 30) || el.getAttribute('aria-label')}" is ${Math.round(r.width)}x${Math.round(r.height)}, under 44x44`);
      }
    }
    return problems;
  }, { parts, phone, touchTargets });
}

for (const { name, path, role, ready, parts, sheet } of PAGES) {
  test(`the ${name} page fits every screen size`, async ({ page }) => {
    test.setTimeout(90_000);
    await mockBackend(page, role);
    const problems: string[] = [];
    for (const [index, [device, width, height]] of SIZES.entries()) {
      await page.setViewportSize({ width, height });
      await page.goto(SITE + path, { waitUntil: 'networkidle' });
      for (const selector of [ready].flat())
        await expect(page.locator(selector).first()).toBeVisible();
      // The web fonts change the layout (and the toolbar is measured again once they are in);
      // table rows fade in.
      await page.evaluate(() => document.fonts.ready);
      await page.waitForTimeout(500);
      // For review: CI puts these on the PR (e2e/contact-sheets.mjs)
      await page.screenshot({ path: `screenshots/${sheet ? `${sheet}/${name}` : name}/${index + 1}-${device}-${width}x${height}.png` });

      const phone = PHONES.includes(device);
      for (const p of await layoutProblems(page, [...HEADER, ...parts], phone, TOUCH_TARGETS))
        problems.push(`${width}x${height}: ${p}`);

      // Where the links are behind the menu, the menu opens with every link on screen.
      const menu = page.locator('.hud-header__menu');
      if (await menu.isVisible()) {
        await menu.click();
        const links = page.locator('mat-sidenav a');
        await expect(links.first()).toBeInViewport();
        const offScreen = await links.evaluateAll(as => as.filter(a => a.getBoundingClientRect().right > innerWidth).length);
        if (offScreen > 0) problems.push(`${width}x${height}: ${offScreen} menu links are off screen`);
        await page.getByRole('button', { name: 'Close menu' }).click();
        await expect(page.locator('mat-sidenav')).toBeHidden();
      }

      // Signed in, the chip opens the user's menu with its items whole on screen, big enough to tap.
      if (role !== 'anon') {
        await page.locator('button.hud-user').click();
        const items = page.getByRole('menuitem');
        await expect(items).toHaveCount(2);
        await expect(items.first()).toBeFocused();
        const itemProblems = await items.evaluateAll(els => els.map(el => {
          const r = el.getBoundingClientRect();
          const label = (el.textContent ?? '').trim();
          if (r.left < 0 || r.right > innerWidth || r.top < 0 || r.bottom > innerHeight) return `${label} is off screen`;
          if (r.height < 43.5) return `${label} is ${Math.round(r.height)}px tall, under 44`;
          return null;
        }).filter(p => p !== null));
        for (const p of itemProblems) problems.push(`${width}x${height}: user menu: ${p}`);
        await page.keyboard.press('Escape');
        await expect(items).toHaveCount(0);
        await expect(page.locator('button.hud-user')).toBeFocused();
      }
    }
    expect(problems).toEqual([]);
  });
}

// The theme editor (the header's palette button) as a dialog at every size: nothing wider than
// the screen, its Close button on screen (and the header it sits in stays there as the editors
// scroll), and closing gives the focus back to the palette button.
test('the theme dialog fits every screen size and closes', async ({ page }) => {
  test.setTimeout(90_000);
  await mockBackend(page, 'anon');
  const problems: string[] = [];
  for (const [index, [device, width, height]] of SIZES.entries()) {
    await page.setViewportSize({ width, height });
    await page.goto(SITE + '/');
    await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();
    const opener = page.getByRole('button', { name: 'Theme icon' });
    await opener.click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.locator('app-palette').first()).toBeVisible();
    await expect(dialog.locator('app-tile').first()).toContainText('#');
    await page.evaluate(() => document.fonts.ready);
    await page.waitForTimeout(300);
    await page.screenshot({ path: `screenshots/theme-dialog/${index + 1}-${device}-${width}x${height}.png` });

    const found = await page.evaluate(() => {
      const out: string[] = [];
      const surface = document.querySelector('.mat-mdc-dialog-surface')!;
      if (document.documentElement.scrollWidth > innerWidth + 1) out.push(`the page scrolls sideways (${document.documentElement.scrollWidth}px)`);
      if (surface.scrollWidth > surface.clientWidth + 1) out.push(`the dialog scrolls sideways (${surface.scrollWidth}px in ${surface.clientWidth}px)`);
      for (const el of surface.querySelectorAll('app-tile, mat-form-field, .theme-head, .theme-foot')) {
        const r = el.getBoundingClientRect();
        if (r.width > 0 && (r.left < -1 || r.right > innerWidth + 1)) out.push(`${el.tagName.toLowerCase()} sticks out (${Math.round(r.left)} to ${Math.round(r.right)})`);
      }
      // Each hex label fits its tile
      for (const hex of surface.querySelectorAll('.tile__hex')) {
        if (hex.scrollWidth > (hex as HTMLElement).clientWidth + 1) out.push(`a hex label is cut off (${hex.textContent})`);
      }
      return out;
    });
    for (const p of found) problems.push(`${width}x${height}: ${p}`);

    // The close button: on screen, big enough to tap, still there after scrolling to the end
    const close = dialog.getByRole('button', { name: 'Close' });
    await dialog.locator('.theme-foot').scrollIntoViewIfNeeded();
    await expect(close).toBeInViewport({ ratio: 1 });
    const box = (await close.boundingBox())!;
    if (box.width < 43.5 || box.height < 43.5) problems.push(`${width}x${height}: Close is ${Math.round(box.width)}x${Math.round(box.height)}`);

    await close.click();
    await expect(dialog).toHaveCount(0);
    await expect(opener).toBeFocused();
  }
  // Done, Esc and a click beside it close it too
  await page.setViewportSize({ width: 1280, height: 720 });
  for (const how of ['Done', 'Escape', 'backdrop'] as const) {
    await page.getByRole('button', { name: 'Theme icon' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    if (how === 'Done') await page.getByRole('dialog').getByRole('button', { name: 'Done' }).click();
    else if (how === 'Escape') await page.keyboard.press('Escape');
    else await page.mouse.click(5, 715);
    await expect(page.getByRole('dialog'), how).toHaveCount(0);
  }
  expect(problems).toEqual([]);
});


// Every state a click reaches (e2e/testPages.mts: each tab, filter, panel, dialog, menu and picker), at every size it
// shows at, screenshotted onto its page's contact sheet. One in the page is checked like a page (its parts and the
// header's on screen and apart, nothing sideways, big enough touch targets on phones); an overlay must fit the screen
// sideways, the page behind it too.
for (const overlay of OVERLAYS) {
  test(`${overlay.name} fits every screen size it shows at`, async ({ page }) => {
    test.setTimeout(90_000);
    await mockBackend(page, overlay.role);
    const problems: string[] = [];
    let shots = 0;
    for (const [index, [device, width, height]] of SIZES.entries()) {
      await page.setViewportSize({ width, height });
      await open(page, overlay.path);
      const opened = await overlay.setup(page);
      if (!opened)
        continue; // Not at this size (the drawer's menu button, where every link fits the header)
      // A state in the page is measured at the top of the page, as a page is: clicking scrolls to what it clicks, and
      // scrolled, the sticky header is over what is under it
      if (overlay.parts)
        await page.evaluate(() => { scrollTo(0, 0); document.querySelector('.mat-drawer-content')?.scrollTo(0, 0); });
      // Its animation done
      await page.waitForTimeout(300);
      const found = overlay.parts
        ? await layoutProblems(page, [...HEADER, ...overlay.parts], PHONES.includes(device), TOUCH_TARGETS)
        : await opened.evaluate(el => {
          const out: string[] = [];
          if (document.documentElement.scrollWidth > innerWidth + 1) out.push(`the page scrolls sideways (${document.documentElement.scrollWidth}px)`);
          const r = el.getBoundingClientRect();
          if (r.left < -1 || r.right > innerWidth + 1) out.push(`it sticks out of the screen (${Math.round(r.left)} to ${Math.round(r.right)})`);
          if (el.scrollWidth > el.clientWidth + 1) out.push(`it scrolls sideways (${el.scrollWidth}px in ${el.clientWidth}px)`);
          return out;
        });
      for (const p of found) problems.push(`${width}x${height}: ${p}`);

      // For the screenshot, a state in the page scrolled to, so it shows
      if (overlay.parts)
        await opened.scrollIntoViewIfNeeded();
      await page.screenshot({ path: `screenshots/${overlay.sheet}/${slug(overlay.name)}/${index + 1}-${device}-${width}x${height}.png` });
      shots++;
      await closeOverlay(page, overlay, opened);
    }
    expect(shots, 'shown at no size').toBeGreaterThan(0);
    expect(problems).toEqual([]);
  });
}
