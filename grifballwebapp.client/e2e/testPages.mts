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
  '/season/13/signupForm', '/season/13/draft', '/season/12/teams', '/season/12/team/1', '/season/12/playergrades', '/seasonmatch/31',
  '/match/0f8fad5b-d9cb-469f-a165-70867728950e',
  '/reschedule-request/31', '/no-such-page',
  // A season backfilled from the historical archive: its banner, champions, stats and what-is-missing notices
  '/season/5', '/season/5?tab=schedule', '/season/5/teams', '/season/5/team/41',
  '/seasons', '/leaderboards',
  // A franchise across seasons (the team page's history link), and Late League: the queue and an archived season
  '/history/team/1', '/late-league', '/late-league/3',
];
export const VISITOR = ['/profile/117', '/login', '/register', '/reset-password?token=e2e-reset-token'];
// The commissioners' and admins' pages (a visitor is sent home from them)
export const SIGNED_IN = [
  '/profile/999', '/seasonManager', '/seasonEdit/0', '/seasonEdit/12', '/seasonAvailability/12', '/commissioner-dashboard',
  '/excel', '/excel/1xGrifballAutumnBrawl2026/Week%203', '/infiniteclient', '/usermanagement', '/usermanagement/createuser',
  '/usermanagement/edituser/2', '/usermanagement/mergeuser', '/usermanagement/mergeuser/2', '/usermanagement/mergeuser/2/3',
  '/usermanagement/claims', '/usermanagement/merges', '/usermanagement/pastgamertags', '/usermanagement/pastgamertags?xboxUserID=2535400000000117',
  '/usermanagement/historicalimport', '/usermanagement/jobs', '/databasebackup',
  // The Telemetry page on its Overview (metrics), and opened on a trace from a link (its waterfall)
  '/usermanagement/telemetry', '/usermanagement/telemetry?trace=4bf92f3577b34da6a3ce929d0e0e4736',
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

/**
 * Opens the quick search from the header's Search button, its input focused. `query`: typed, then waited on until
 * the list (or, for a failing search, the Try again button) shows.
 */
export async function openQuickSearch(page: Page, query?: string): Promise<Locator> {
  await page.locator('header').getByRole('button', { name: 'Search', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Quick search' });
  const input = dialog.getByRole('combobox');
  await expect(input).toBeFocused();
  if (query !== undefined) {
    await input.fill(query);
    await expect(dialog.locator('.qs-status')).not.toContainText('Searching');
    await expect(dialog.locator('.qs-item, .qs-retry, .qs-status:has-text("Nothing matches")').first()).toBeVisible();
  }
  return dialog;
}

/** The quick search's answer replaced, for the states the mock API does not give (none found, a failure). */
async function answerQuickSearch(page: Page, answer: { status: number, body: string }): Promise<void> {
  await page.route('**/api/Search/QuickSearch**', route => route.fulfill({ ...answer, contentType: 'application/json' }));
}

/**
 * Types `query` in the leaderboards' player search and waits for its suggestions (the mock answers the same players to
 * anything: the two Old Timers, current gamertag and archive, and two more).
 */
export async function openPlayerSearch(page: Page, query = 'old'): Promise<Locator> {
  await page.getByRole('combobox', { name: 'Find player standing' }).fill(query);
  const list = page.getByRole('listbox', { name: 'Players found' });
  await expect(list.getByRole('option')).toHaveCount(4);
  return list;
}

/** The match page's box score on one game (its tab), as a commissioner sees it: the game's Edit and Unreport. */
async function showGame(page: Page, game: number): Promise<void> {
  await page.getByRole('button', { name: `Game ${game}`, exact: true }).click();
  await expect(page.getByRole('button', { name: `Unreport game ${game}` })).toBeVisible();
}

/** Opens the match page's "Enter a game by hand" form. */
export async function openManualGameForm(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'Enter a game by hand' }).click();
  const form = page.getByRole('form', { name: 'Manual game entry' });
  await expect(form.getByRole('checkbox').first()).toBeVisible();
  return form;
}

/** Asks to unreport game 2 (entered by hand) from the box score: its confirmation. */
export async function openUnreportConfirmation(page: Page): Promise<Locator> {
  await showGame(page, 2);
  await page.getByRole('button', { name: 'Unreport game 2' }).click();
  const confirm = page.getByRole('group', { name: 'Confirm unreporting game 2' });
  await expect(confirm).toContainText('entered by hand');
  return confirm;
}

/** Reports a possible match from Waypoint to the series (which has games): the choice of new game or replacement. */
export async function openReplaceChoice(page: Page): Promise<Locator> {
  await page.locator('.lobby-possible').getByRole('button', { name: 'SUBMIT' }).click();
  const choice = page.getByRole('group', { name: 'Is this a new game, or does it replace one?' });
  await expect(choice.getByRole('button', { name: 'Replaces game 2 (entered by hand)' })).toBeVisible();
  return choice;
}

/** Closes one of the match page's panels (not dialogs: Esc does nothing there) with its own Cancel. */
export async function cancelPanel(page: Page, panel: Locator): Promise<void> {
  await panel.getByRole('button', { name: 'Cancel' }).click();
  await expect(panel).toHaveCount(0);
}

/** The queue panel, its status answered with `changes` (the mock's: open, joinable signed in), the page loaded again. */
async function queueAs(page: Page, changes: Record<string, unknown>): Promise<Locator> {
  await page.route('**/api/Queue/Status', route => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ ...QUEUE_STATUS, ...changes }) }));
  await open(page, page.url().slice(SITE.length));
  return page.locator('app-late-league-queue');
}

/** The queue as a signed-in player with a gamertag sees it (mockApi.mts's /api/Queue/Status). */
const QUEUE_STATUS = { count: 6, open: true, signedIn: true, hasGamertag: true, inQueue: false, inMatch: false };

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
  // The quick search (the header's Search button, or Ctrl+K): before anything is typed, signed out and in
  {
    name: 'quick search', role: 'anon', path: '/',
    setup: page => openQuickSearch(page),
  },
  {
    name: 'quick search, signed in', role: 'admin', path: '/season/12',
    setup: page => openQuickSearch(page),
  },
  {
    // Players (two of one name, one found by a past gamertag, one by their Discord name), teams and seasons
    name: 'quick search, results', role: 'anon', path: '/',
    setup: page => openQuickSearch(page, 'old'),
  },
  {
    name: 'quick search, nothing found', role: 'anon', path: '/',
    setup: async page => {
      await answerQuickSearch(page, { status: 200, body: JSON.stringify({ players: [], teams: [], seasons: [] }) });
      return openQuickSearch(page, 'zzzz');
    },
  },
  {
    name: 'quick search, failed', role: 'anon', path: '/',
    setup: async page => {
      await answerQuickSearch(page, { status: 500, body: '' });
      const dialog = await openQuickSearch(page, 'old');
      await expect(dialog.getByRole('button', { name: 'Try again' })).toBeVisible();
      return dialog;
    },
  },
  // The leaderboards' player search: its suggestions (two players of one name, labelled Current gamertag and Archive),
  // and the standing of the player picked
  {
    name: 'leaderboards, player suggestions', role: 'anon', path: '/leaderboards',
    setup: page => openPlayerSearch(page),
    close: async page => {
      await page.keyboard.press('Escape');
      await expect(page.getByRole('listbox', { name: 'Players found' })).toBeHidden();
    },
  },
  {
    // Not a dialog: the standing shown above the boards
    name: 'leaderboards, player spotlight', role: 'admin', path: '/leaderboards',
    setup: async page => {
      const list = await openPlayerSearch(page, 'legend');
      await list.getByRole('option', { name: /Legendary/ }).click();
      const spotlight = page.locator('app-player-spotlight');
      await expect(spotlight).toContainText('Legendary');
      return spotlight;
    },
    close: null,
  },
  // The match page's commissioner tools (#267), on a series with a Waypoint game and one entered by hand: entering a
  // game by hand (and its "This game is" list), editing one, unreporting one, and reporting a Waypoint game to it
  {
    name: 'match, enter a game by hand', role: 'admin', path: '/seasonmatch/31',
    setup: openManualGameForm,
    close: page => cancelPanel(page, page.getByRole('form', { name: 'Manual game entry' })),
  },
  {
    name: 'match, enter a game by hand, new or replacing', role: 'admin', path: '/seasonmatch/31',
    setup: async page => {
      const form = await openManualGameForm(page);
      await form.getByRole('combobox', { name: 'This game is' }).click();
      const listbox = page.getByRole('listbox');
      await expect(listbox.getByRole('option')).toHaveText(['A new game (game 3)', 'Replaces game 1 (from Waypoint)', 'Replaces game 2 (entered by hand)']);
      return listbox;
    },
  },
  {
    name: 'match, edit a game entered by hand', role: 'admin', path: '/seasonmatch/31',
    setup: async page => {
      await showGame(page, 2);
      await page.getByRole('button', { name: 'Edit game 2' }).click();
      const form = page.getByRole('form', { name: 'Manual game entry' });
      await expect(form.getByRole('textbox', { name: 'Map' })).toHaveValue('Bazaar');
      return form;
    },
    close: page => cancelPanel(page, page.getByRole('form', { name: 'Manual game entry' })),
  },
  {
    name: 'match, unreport confirmation', role: 'admin', path: '/seasonmatch/31',
    setup: openUnreportConfirmation,
    close: page => cancelPanel(page, page.getByRole('group', { name: 'Confirm unreporting game 2' })),
  },
  {
    name: 'match, report a Waypoint game: new or replacing', role: 'admin', path: '/seasonmatch/31',
    setup: openReplaceChoice,
    close: page => cancelPanel(page, page.getByRole('group', { name: 'Is this a new game, or does it replace one?' })),
  },
  // The Late League queue panel (#269) in the states the mock's status does not show (signed out and joinable it
  // shows on every page it is on): in the queue (joined from it), in a match, without a gamertag, and Late League off
  {
    name: 'Late League queue, joined', role: 'player', path: '/',
    setup: async page => {
      await page.route('**/api/Queue/Join', route => route.fulfill({ status: 200, contentType: 'application/json',
        body: JSON.stringify({ ...QUEUE_STATUS, count: 7, inQueue: true }) }));
      const panel = page.locator('app-late-league-queue');
      await panel.getByRole('button', { name: 'Join the queue' }).click();
      await expect(panel.getByRole('button', { name: 'Leave the queue' })).toBeVisible();
      await expect(panel).toContainText('You have joined the Late League queue');
      return panel;
    },
    close: null,
  },
  {
    name: 'Late League queue, in a match', role: 'player', path: '/late-league',
    setup: async page => {
      const panel = await queueAs(page, { inMatch: true });
      await expect(panel).toContainText('You are in a Late League match.');
      return panel;
    },
    close: null,
  },
  {
    name: 'Late League queue, no gamertag', role: 'player', path: '/',
    setup: async page => {
      const panel = await queueAs(page, { hasGamertag: false });
      await expect(panel).toContainText('Set your gamertag');
      return panel;
    },
    close: null,
  },
  {
    name: 'Late League queue, Late League off', role: 'player', path: '/late-league',
    setup: async page => {
      const panel = await queueAs(page, { open: false, count: 0 });
      await expect(panel).toContainText('Late League is off here.');
      return panel;
    },
    close: null,
  },
  {
    // Not a dialog: a log line opened on the Telemetry page's Logs tab, its properties and links to its trace
    name: 'telemetry, log line', role: 'admin', path: '/usermanagement/telemetry',
    setup: async page => {
      await page.getByRole('button', { name: 'Logs', exact: true }).click();
      await page.locator('.tm-log-line').first().click();
      const detail = page.locator('.tm-detail');
      await expect(detail).toContainText('MatchPullService');
      return detail;
    },
    close: null,
  },
  {
    // Not a dialog: a span opened in a trace's waterfall (its attributes and its exception event)
    name: 'telemetry, trace span', role: 'admin', path: '/usermanagement/telemetry?trace=4bf92f3577b34da6a3ce929d0e0e4736',
    setup: async page => {
      await page.locator('.tm-span').first().click();
      const detail = page.locator('.tm-detail');
      await expect(detail).toContainText('HttpRequestException');
      return detail;
    },
    close: null,
  },
  {
    name: 'database backup, format list', role: 'admin', path: '/databasebackup',
    setup: async page => {
      await page.getByRole('combobox', { name: 'Format' }).click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toContainText('PostgreSQL dump');
      return listbox;
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
    // Not a dialog: the job panel the page opens above its jobs list (a guarded import with seasons to decide on)
    name: 'historical import, job details', role: 'admin', path: '/usermanagement/historicalimport',
    setup: async page => {
      await page.getByRole('button', { name: 'Show job 41' }).click();
      const panel = page.locator('.hi-job');
      await expect(panel).toContainText('Needs review');
      return panel;
    },
    close: null,
  },
  {
    // Not a dialog: the panel the Jobs page opens under its list (a job's schedule editor and runs)
    name: 'jobs, job details', role: 'admin', path: '/usermanagement/jobs',
    setup: async page => {
      await page.getByRole('button', { name: 'Details of historical-refresh' }).click();
      const panel = page.locator('.jb-detail');
      await expect(panel).toContainText('Recent runs');
      return panel;
    },
    close: null,
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
