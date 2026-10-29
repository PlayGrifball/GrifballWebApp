import { expect, test, type Page } from '@playwright/test';
import { META, MOCK_NOW, mockResponse, type Role } from './mockApi.mts';

// Every key page at every screen size: no sideways scrolling, its main parts on screen and not on
// top of one another, the navigation reachable, and big enough touch targets on phones. Pages
// scroll down, so only the horizontal direction counts. The backend is mocked (e2e/mockApi.mts).
//
// Each page and size is screenshotted to screenshots/<page>/<n>-<device>-<width>x<height>.png;
// CI turns them into one contact sheet per page (e2e/contact-sheets.mjs) and links them on the PR.

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
  /** Shown once the page's data has loaded. */
  ready: string;
  /** The parts that must each be on screen (horizontally) and must not overlap one another.
   * "text:" measures an element's text rather than its box, e.g. a heading in a wide box. */
  parts: string[];
}

const TABS = ['.hud-tab'];

const PAGES: PageCase[] = [
  { name: 'home', path: '/', role: 'player', ready: 'tr.mat-mdc-row',
    parts: ['.home-hero .hud-kicker', 'text:.home-hero h1', '.home-hero .hud-lede', '.home-hero__actions a', 'main h2', 'a.hud-card', '.hud-table-wrap'] },
  { name: 'home-signed-out', path: '/', role: 'anon', ready: 'tr.mat-mdc-row',
    parts: ['text:.home-hero h1', '.home-hero__actions a', 'a.hud-card'] },
  { name: 'season', path: '/season/12', role: 'player', ready: '.week-card',
    parts: ['text:.season-head h1', '.season-head .hud-actions a', ...TABS, 'main h2', 'app-team-standings', '.week-card', '.hud-more'] },
  { name: 'season-schedule', path: '/season/12?tab=schedule', role: 'player', ready: '.sched-match',
    parts: ['text:.season-head h1', ...TABS, '.sched-week'] },
  { name: 'season-bracket', path: '/season/12?tab=bracket', role: 'player', ready: '.brackets-viewer .match',
    parts: ['text:.season-head h1', ...TABS, '.bracket-box', '.bracket-legend > span'] },
  { name: 'match', path: '/seasonmatch/31', role: 'player', ready: '.lobby-player',
    parts: ['text:.lobby-head h1', '.lobby-head .hud-actions > *', '.lobby-team__banner', '.lobby-player', '.lobby-score', '.lobby-info > section'] },
  { name: 'profile', path: '/profile/117', role: 'player', ready: '.linked-login',
    parts: ['.profile-card', '.profile-main > section'] },
  { name: 'signup', path: '/season/13/signupForm', role: 'player', ready: '.enlist-step',
    parts: ['.enlist-form', '.enlist-timeline h2', '.enlist-step', '.enlist-timeline .hud-more'] },
  { name: 'login', path: '/login', role: 'anon', ready: '.login-panel',
    parts: ['.login-panel', '.login-side > *'] },
  { name: 'user-management', path: '/usermanagement', role: 'admin', ready: 'app-table tr.mat-mdc-row',
    parts: ['text:app-user-management > h1', 'app-user-management > button', 'app-search-box', 'app-table'] },
  { name: 'season-manager', path: '/seasonManager', role: 'admin', ready: 'tr.mat-mdc-row',
    parts: ['text:app-season-manager > h1', 'app-season-manager > button', 'app-season-manager .hud-table-wrap'] },
];

// What has to be at least 44x44 on a phone: the buttons and links people tap.
const TOUCH_TARGETS = [
  'header a:not(#hidden)', 'header button', '.hud-btn', '.hud-tab', '.hud-more', 'a.hud-card',
  'main .mat-mdc-button-base', '.home-archive__link', '.standings__team', '.sched-match__link', '.hud-cell-btn',
];

/** Logs in as `role` (nothing for a visitor), freezes the clock and mocks the backend. */
async function mockBackend(page: Page, role: Role): Promise<void> {
  if (role !== 'anon') {
    await page.addInitScript(meta => {
      localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'e2e-token', expiresIn: 3600, refreshToken: 'e2e-refresh' }));
      localStorage.setItem('metaInfo', JSON.stringify(meta));
    }, META[role]);
  }
  await page.clock.setFixedTime(new Date(MOCK_NOW));
  // Hermetic: the app and its fonts are all served locally.
  await page.route(url => url.hostname !== '127.0.0.1', route => route.abort());
  await page.route('**/hub/**', route => route.fulfill({ status: 404, body: '' }));
  await page.route('**/api/**', route => {
    const mocked = mockResponse(role, new URL(route.request().url()).pathname);
    return mocked ? route.fulfill(mocked) : route.fulfill({ status: 404, body: '' });
  });
}

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

for (const { name, path, role, ready, parts } of PAGES) {
  test(`the ${name} page fits every screen size`, async ({ page }) => {
    test.setTimeout(90_000);
    await mockBackend(page, role);
    const problems: string[] = [];
    for (const [index, [device, width, height]] of SIZES.entries()) {
      await page.setViewportSize({ width, height });
      await page.goto(path);
      await expect(page.locator(ready).first()).toBeVisible();
      // The web fonts change the layout (and the toolbar is measured again once they are in);
      // table rows fade in.
      await page.evaluate(() => document.fonts.ready);
      await page.waitForTimeout(500);
      // For review: CI puts these on the PR (e2e/contact-sheets.mjs)
      await page.screenshot({ path: `screenshots/${name}/${index + 1}-${device}-${width}x${height}.png` });

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
    }
    expect(problems).toEqual([]);
  });
}
