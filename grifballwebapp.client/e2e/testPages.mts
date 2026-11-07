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
  // A Waypoint match the site has no record of (a link to it from elsewhere): its "not found"
  '/match/00000000-0000-0000-0000-000000000000',
  '/reschedule-request/31', '/no-such-page',
  // A season backfilled from the historical archive: its banner, champions, stats and what-is-missing notices
  '/season/5', '/season/5?tab=schedule', '/season/5?tab=bracket', '/season/5/teams', '/season/5/team/41',
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
    const url = new URL(route.request().url());
    const mocked = mockResponse(role, url.pathname, url.search);
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
 * A state of a page that a click (or typing) reaches without a new route: a dialog, menu or picker open, or in the page
 * itself a tab, a filter, a panel, an empty or error state. Each is checked by axe (a11y.spec.ts) and the design rules
 * (design-rules.spec.ts), and screenshotted at every size and checked to fit (layout.spec.ts). A new tab is one entry.
 *
 * `role` and `path` are what to mock and open first, `setup` reaches the state (clicking, hovering, typing, ...) and
 * returns the locator now shown, or null if it does not apply at the current viewport (the nav drawer's button only
 * shows on narrow screens). `close`: undefined closes with Escape (and checks the overlay is gone); a function replaces
 * that with its own close-and-check; null (a tooltip, or a state of the page itself) does neither, since the next
 * navigation clears it.
 *
 * `sheet`: the contact sheet its screenshots go on (screenshots/<sheet>/<name>/, e2e/contact-sheets.mjs), the page's
 * it opens on, so a page's tabs and dialogs are reviewed together. `parts`: for a state in the page (a tab, a panel),
 * its main parts, checked like a page's are (on screen, not on top of one another, the header's parts too); without
 * them it is an overlay above the page, and only it is checked to fit the screen.
 */
export interface Overlay {
  name: string;
  role: Role;
  path: string;
  sheet: string;
  parts?: string[];
  setup(page: Page): Promise<Locator | null>;
  close?: ((page: Page) => Promise<void>) | null;
}

/** The page's heading, the part every state in a page has (`Overlay.parts`). */
export const HEADING = 'text:main h1';
/** The page's tab buttons or links (`Overlay.parts`). */
export const TABS = '.hud-tab';

/**
 * Picks the tab named `name` in a row of tab buttons (`.hud-tabs`: a role="group", or a <nav>) and waits for it to be
 * the one pressed.
 */
export async function pickTab(tabs: Locator, name: string): Promise<void> {
  const tab = tabs.getByRole('button', { name, exact: true });
  await tab.click();
  await expect(tab).toHaveAttribute('aria-pressed', 'true');
}

/** A state's screenshots' folder name: its name, lower case, words joined by dashes. */
export function slug(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
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
/** The queue panel's count of players, shown in every state but a failed load. */
const QUEUE_COUNT = '.late-league-queue__count';

async function openThemeDialog(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'Theme icon' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.locator('app-tile').first()).toContainText('#');
  return dialog;
}

/** Opens the navigation drawer from the header's menu button, which only shows where the links do not all fit. */
async function openDrawer(page: Page): Promise<Locator | null> {
  const menuButton = page.locator('.hud-header__menu');
  if (!(await menuButton.isVisible()))
    return null;
  await menuButton.click();
  const drawer = page.locator('mat-sidenav');
  await expect(page.locator('mat-sidenav a').first()).toBeInViewport();
  // Slid all the way in
  await expect(drawer).toHaveCSS('transform', 'none');
  return drawer;
}

async function closeDrawer(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Close menu' }).click();
  await expect(page.locator('mat-sidenav')).toBeHidden();
}

/** Closes the quick search with Esc: it is gone, and the focus is back on the header's Search button. */
async function closeQuickSearch(page: Page): Promise<void> {
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Quick search' })).toHaveCount(0);
  await expect(page.locator('header').getByRole('button', { name: 'Search', exact: true })).toBeFocused();
}

/** Picks one of the leaderboards' boards: its table is the one shown, headed by `column`. */
async function showBoard(page: Page, board: string, column: string): Promise<Locator> {
  await pickTab(page.getByRole('group', { name: 'Leaderboard' }), board);
  const table = page.locator('app-leaderboards .hud-table-wrap');
  await expect(table.locator('thead')).toContainText(column);
  return table;
}

/** Opens one of the Telemetry page's tabs (the page is on Overview). */
async function telemetryTab(page: Page, tab: 'Logs' | 'Traces'): Promise<void> {
  await pickTab(page.getByRole('group', { name: 'Telemetry' }), tab);
}

/** Opens the Telemetry page's Logs tab and the first (the error's) log line. */
async function openLogLine(page: Page): Promise<Locator> {
  await telemetryTab(page, 'Logs');
  await page.locator('.tm-log-line').first().click();
  const detail = page.locator('.tm-detail');
  await expect(detail).toContainText('MatchPullService');
  return detail;
}

/** Shows the claims of one status (the gamertag claims page's tabs); `text`: in the claims then shown. */
async function showClaims(page: Page, status: 'Approved' | 'Rejected', text: string): Promise<Locator> {
  await pickTab(page.getByRole('group', { name: 'Claim status' }), status);
  const table = page.getByRole('region', { name: 'Gamertag claims', exact: true });
  await expect(table).toContainText(text);
  return table;
}

/** The archive season 5's player stats: a phase picked, or a filter typed. */
const SEASON_STATS = 'app-season-player-stats';

/** Types in a page's search box and waits for what it shows then (`shown`). */
async function search(box: Locator, query: string, shown: Locator): Promise<Locator> {
  await box.fill(query);
  await expect(shown.first()).toBeVisible();
  return shown.first();
}

export const OVERLAYS: Overlay[] = [
  // The header's: the theme editor, the user's menu, the navigation drawer and the quick search
  {
    name: 'theme dialog', role: 'anon', path: '/', sheet: 'theme-dialog',
    setup: openThemeDialog,
  },
  {
    name: 'theme dialog, font list', role: 'anon', path: '/', sheet: 'theme-dialog',
    setup: async page => {
      const dialog = await openThemeDialog(page);
      await dialog.getByRole('combobox').click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toBeVisible();
      return listbox;
    },
  },
  {
    name: 'theme dialog, colour picker', role: 'anon', path: '/', sheet: 'theme-dialog',
    setup: async page => {
      const dialog = await openThemeDialog(page);
      await dialog.getByRole('button', { name: 'Pick the primary colour' }).click();
      const picker = page.locator('mtx-colorpicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'user menu', role: 'admin', path: '/', sheet: 'home',
    setup: async page => {
      await page.locator('button.hud-user').click();
      const menu = page.getByRole('menu');
      await expect(page.getByRole('menuitem').first()).toBeFocused();
      return menu;
    },
  },
  {
    // The menu button is there when the links do not all fit in the header (on phones); at wider sizes there is
    // nothing to open.
    name: 'navigation drawer', role: 'admin', path: '/', sheet: 'home',
    setup: openDrawer,
    close: closeDrawer,
  },
  {
    name: 'navigation drawer, signed out', role: 'anon', path: '/', sheet: 'home-signed-out',
    setup: openDrawer,
    close: closeDrawer,
  },
  // The quick search (the header's Search button, or Ctrl+K): before anything is typed, signed out and in
  {
    name: 'quick search', role: 'anon', path: '/', sheet: 'home-signed-out',
    setup: page => openQuickSearch(page),
    close: closeQuickSearch,
  },
  {
    name: 'quick search, signed in', role: 'admin', path: '/season/12', sheet: 'season',
    setup: page => openQuickSearch(page),
    close: closeQuickSearch,
  },
  {
    // Players (two of one name, one found by a past gamertag, one by their Discord name), teams and seasons
    name: 'quick search, results', role: 'anon', path: '/', sheet: 'home-signed-out',
    setup: page => openQuickSearch(page, 'old'),
    close: closeQuickSearch,
  },
  {
    name: 'quick search, nothing found', role: 'anon', path: '/', sheet: 'home-signed-out',
    setup: async page => {
      await answerQuickSearch(page, { status: 200, body: JSON.stringify({ players: [], teams: [], seasons: [] }) });
      return openQuickSearch(page, 'zzzz');
    },
    close: closeQuickSearch,
  },
  {
    name: 'quick search, failed', role: 'anon', path: '/', sheet: 'home-signed-out',
    setup: async page => {
      await answerQuickSearch(page, { status: 500, body: '' });
      const dialog = await openQuickSearch(page, 'old');
      await expect(dialog.getByRole('button', { name: 'Try again' })).toBeVisible();
      return dialog;
    },
    close: closeQuickSearch,
  },
  // The leaderboards: each board (Championships is the page as it opens), the titles of one format, the player search's
  // suggestions (two players of one name, labelled Current gamertag and Archive), the standing of the player picked,
  // and a search that finds nobody
  ...([['Dynasties', 'Team'], ['Goals', 'Goals'], ['Kills', 'Kills'], ['Games played', 'Games played'], ['Carrier stops', 'Carrier stops']] as const)
    .map(([board, column]): Overlay => ({
      name: `leaderboards, ${board}`, role: 'anon', path: '/leaderboards', sheet: 'leaderboards',
      parts: [HEADING, TABS, 'app-leaderboards .hud-table-wrap'],
      setup: page => showBoard(page, board, column),
      close: null,
    })),
  {
    name: 'leaderboards, tournament titles', role: 'anon', path: '/leaderboards', sheet: 'leaderboards',
    parts: [HEADING, TABS, 'app-leaderboards .hud-table-wrap'],
    setup: async page => {
      await page.locator('app-leaderboards .board-filter select').selectOption('Tournament');
      const table = page.locator('app-leaderboards .hud-table-wrap');
      await expect(table).toContainText('Harvest Hammer');
      return table;
    },
    close: null,
  },
  {
    name: 'leaderboards, player suggestions', role: 'anon', path: '/leaderboards', sheet: 'leaderboards',
    setup: page => openPlayerSearch(page),
    close: async page => {
      await page.keyboard.press('Escape');
      await expect(page.getByRole('listbox', { name: 'Players found' })).toBeHidden();
    },
  },
  {
    // Not a dialog: the standing shown above the boards
    name: 'leaderboards, player spotlight', role: 'admin', path: '/leaderboards', sheet: 'leaderboards',
    parts: [HEADING, TABS, 'app-player-spotlight'],
    setup: async page => {
      const list = await openPlayerSearch(page, 'legend');
      await list.getByRole('option', { name: /Legendary/ }).click();
      const spotlight = page.locator('app-player-spotlight');
      await expect(spotlight).toContainText('Legendary');
      return spotlight;
    },
    close: null,
  },
  {
    name: 'leaderboards, no player found', role: 'anon', path: '/leaderboards', sheet: 'leaderboards',
    parts: [HEADING, TABS],
    setup: async page => {
      await page.route('**/api/Leaderboards/GetPlayerRank**', route => route.fulfill({ status: 204, body: '' }));
      await page.getByRole('combobox', { name: 'Find player standing' }).fill('nobody at all');
      // The suggestions (the mock's players, whatever is typed) closed: the notice is not under them
      await page.keyboard.press('Escape');
      await page.locator('app-player-search').getByRole('button', { name: 'Search', exact: true }).click();
      const notice = page.locator('app-leaderboards .hud-empty');
      await expect(notice).toContainText('No player found matching "nobody at all"');
      return notice;
    },
    close: null,
  },
  // The seasons list: searched, filtered to one game, and nothing matching
  {
    name: 'seasons, searched', role: 'player', path: '/seasons', sheet: 'seasons',
    parts: [HEADING, '.season-card'],
    setup: page => search(page.locator('.seasons-filter--search input'), 'summer', page.locator('.season-card').filter({ hasText: 'Summer Smash' })),
    close: null,
  },
  {
    name: 'seasons, one game', role: 'player', path: '/seasons', sheet: 'seasons',
    parts: [HEADING, '.season-card'],
    setup: async page => {
      // The server filters the list (?gameVersionID=): the mock's seasons of that game
      await page.route('**/api/Season/GetSeasons**', route => {
        const { results } = JSON.parse(mockResponse('player', '/api/Season/GetSeasons')!.body) as { results: { gameVersionID?: number }[] };
        const halo3 = results.filter(s => s.gameVersionID === 1);
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ totalCount: halo3.length, results: halo3 }) });
      });
      await page.locator('.seasons-filters select').nth(1).selectOption({ label: 'Halo 3' });
      const cards = page.locator('.season-card');
      await expect(cards).toHaveCount(1);
      return cards.first();
    },
    close: null,
  },
  {
    name: 'seasons, nothing matches', role: 'player', path: '/seasons', sheet: 'seasons',
    parts: [HEADING],
    setup: page => search(page.locator('.seasons-filter--search input'), 'zzzz', page.getByText('No seasons match.')),
    close: null,
  },
  // A season's player stats (the archive's season 5, which has them): a phase, sorted by goals, and none matching
  {
    name: 'season stats, playoffs', role: 'player', path: '/season/5', sheet: 'season-archive',
    parts: [HEADING, TABS, `${SEASON_STATS} .hud-table-wrap`],
    setup: async page => {
      await pickTab(page.getByRole('group', { name: 'Phase' }), 'Playoffs');
      const table = page.locator(`${SEASON_STATS} .hud-table-wrap`);
      await expect(table.locator('tbody tr')).toHaveCount(1);
      return table;
    },
    close: null,
  },
  {
    name: 'season stats, regular season by goals', role: 'player', path: '/season/5', sheet: 'season-archive',
    parts: [HEADING, TABS, `${SEASON_STATS} .hud-table-wrap`],
    setup: async page => {
      await pickTab(page.getByRole('group', { name: 'Phase' }), 'Regular season');
      await page.locator(SEASON_STATS).getByRole('button', { name: 'Goals', exact: true }).click();
      const table = page.locator(`${SEASON_STATS} .hud-table-wrap`);
      await expect(table.locator('th[aria-sort]').filter({ hasText: 'Goals' })).not.toHaveAttribute('aria-sort', 'none');
      return table;
    },
    close: null,
  },
  {
    name: 'season stats, no player matches', role: 'player', path: '/season/5', sheet: 'season-archive',
    parts: [HEADING, TABS],
    setup: page => search(page.getByRole('searchbox', { name: 'Filter players by gamertag or team' }), 'zzzz', page.getByText('No players match.')),
    close: null,
  },
  // A season's teams, searched: some found, and none
  {
    name: 'season teams, searched', role: 'player', path: '/season/12/teams', sheet: 'season-teams',
    parts: [HEADING, '.teams-toolbar', '.team-card'],
    setup: async page => {
      const card = await search(page.getByRole('searchbox', { name: 'Search teams or players' }), 'red', page.locator('.team-card'));
      await expect(page.locator('.teams-toolbar')).toContainText('Showing 1 of 6 teams');
      return card;
    },
    close: null,
  },
  {
    name: 'season teams, nothing found', role: 'player', path: '/season/12/teams', sheet: 'season-teams',
    parts: [HEADING, '.teams-toolbar'],
    setup: page => search(page.getByRole('searchbox', { name: 'Search teams or players' }), 'zzzz',
      page.locator('main').getByRole('button', { name: 'Clear search', exact: true }).last()),
    close: null,
  },
  // A profile: the service record's League and Late League totals, each game's era, and (on one's own) claiming a
  // past gamertag
  ...(['League', 'Late League'] as const).map((scope): Overlay => ({
    name: `profile, ${scope} record`, role: 'player', path: '/profile/117', sheet: 'profile',
    parts: [HEADING, 'app-service-record'],
    setup: async page => {
      await pickTab(page.getByRole('group', { name: 'Service record games' }), scope);
      return page.locator('app-service-record');
    },
    close: null,
  })),
  ...(['Halo 3', 'Halo Infinite'] as const).map((era): Overlay => ({
    name: `profile, ${era} era`, role: 'anon', path: '/profile/117', sheet: 'profile',
    parts: [HEADING, '.stat-card-grid'],
    setup: async page => {
      await pickTab(page.getByRole('navigation', { name: 'Game era filter' }), era);
      const cards = page.locator('.stat-card-grid');
      await expect(cards).toContainText(era === 'Halo 3' ? '8 - 2' : '27 - 15');
      return cards;
    },
    close: null,
  })),
  {
    name: 'profile, claiming a past gamertag', role: 'player', path: '/profile/117', sheet: 'profile',
    parts: [HEADING, '.pi-preview'],
    setup: async page => {
      await page.getByLabel('Old gamertag').fill('Old Timer');
      await page.getByRole('button', { name: 'Find its records' }).click();
      const preview = page.locator('.pi-preview');
      await expect(preview).toContainText('Summer League 2010');
      return preview;
    },
    close: null,
  },
  // The match page's box score on each game (it opens on the series totals), as anyone sees it
  ...([['Game 1', 'Waypoint ID'], ['Game 2', 'Entered by hand']] as const).map(([game, meta]): Overlay => ({
    name: `match, ${game}`, role: 'player', path: '/seasonmatch/31', sheet: 'match',
    parts: [HEADING, TABS, '.lobby-scoreboard'],
    setup: async page => {
      await pickTab(page.getByRole('group', { name: 'Scoreboard selection' }), game);
      const board = page.locator('.lobby-scoreboard');
      await expect(board.locator('.lobby-game-meta')).toContainText(meta);
      return board;
    },
    close: null,
  })),
  // The match page's commissioner tools (#267), on a series with a Waypoint game and one entered by hand: entering a
  // game by hand (and its "This game is" list), editing one, unreporting one, and reporting a Waypoint game to it; and
  // a forfeit asked for, waiting for its Confirm
  {
    name: 'match, enter a game by hand', role: 'admin', path: '/seasonmatch/31', sheet: 'match-commissioner',
    setup: openManualGameForm,
    close: page => cancelPanel(page, page.getByRole('form', { name: 'Manual game entry' })),
  },
  {
    name: 'match, enter a game by hand, new or replacing', role: 'admin', path: '/seasonmatch/31', sheet: 'match-commissioner',
    setup: async page => {
      const form = await openManualGameForm(page);
      await form.getByRole('combobox', { name: 'This game is' }).click();
      const listbox = page.getByRole('listbox');
      await expect(listbox.getByRole('option')).toHaveText(['A new game (game 3)', 'Replaces game 1 (from Waypoint)', 'Replaces game 2 (entered by hand)']);
      return listbox;
    },
  },
  {
    name: 'match, edit a game entered by hand', role: 'admin', path: '/seasonmatch/31', sheet: 'match-commissioner',
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
    name: 'match, unreport confirmation', role: 'admin', path: '/seasonmatch/31', sheet: 'match-commissioner',
    setup: openUnreportConfirmation,
    close: page => cancelPanel(page, page.getByRole('group', { name: 'Confirm unreporting game 2' })),
  },
  {
    name: 'match, report a Waypoint game: new or replacing', role: 'admin', path: '/seasonmatch/31', sheet: 'match-commissioner',
    setup: openReplaceChoice,
    close: page => cancelPanel(page, page.getByRole('group', { name: 'Is this a new game, or does it replace one?' })),
  },
  {
    name: 'match, forfeit confirmation', role: 'admin', path: '/seasonmatch/31', sheet: 'match-commissioner',
    parts: [HEADING, '.lobby-admin'],
    setup: async page => {
      await page.getByRole('button', { name: 'Home Team Forfeit' }).click();
      const panel = page.locator('.lobby-admin');
      await expect(panel).toContainText('The home team forfeits and the away team wins.');
      await expect(panel.getByRole('button', { name: 'Confirm' })).toBeVisible();
      return panel;
    },
    close: async page => {
      await page.locator('.lobby-admin').getByRole('button', { name: 'Cancel' }).click();
      await expect(page.getByRole('button', { name: 'Home Team Forfeit' })).toBeVisible();
    },
  },
  // A match its games decided (the server's decidedBy and canUndo): its Match report, and Undo Result waiting for its
  // Confirm
  {
    name: 'match, decided: undo confirmation', role: 'admin', path: '/seasonmatch/31', sheet: 'match-commissioner',
    parts: [HEADING, '.lobby-admin'],
    setup: async page => {
      const path = '/api/SeasonMatch/GetSeasonMatchPage/31';
      const undecided = JSON.parse(mockResponse('admin', path)!.body);
      await page.route('**' + path, route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
        ...undecided, homeTeamScore: 2, homeTeamResult: 'Won', awayTeamResult: 'Loss', decidedBy: 'Games', canUndo: true,
      }) }));
      await open(page, page.url().slice(SITE.length));
      await expect(page.locator('main h1')).toHaveText('Match report');
      await page.getByRole('button', { name: 'Undo Result' }).click();
      const panel = page.locator('.lobby-admin');
      await expect(panel).toContainText('This match goes back to undecided');
      return panel;
    },
    close: null,
  },
  // The Late League queue panel (#269) in the states the mock's status does not show (signed out and joinable it
  // shows on every page it is on): in the queue (joined from it), in a match, without a gamertag, and Late League off
  {
    name: 'Late League queue, joined', role: 'player', path: '/', sheet: 'home',
    parts: [HEADING, QUEUE_COUNT],
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
    name: 'Late League queue, in a match', role: 'player', path: '/late-league', sheet: 'late-league',
    parts: [HEADING, QUEUE_COUNT],
    setup: async page => {
      const panel = await queueAs(page, { inMatch: true });
      await expect(panel).toContainText('You are in a Late League match.');
      return panel;
    },
    close: null,
  },
  {
    name: 'Late League queue, no gamertag', role: 'player', path: '/', sheet: 'home',
    parts: [HEADING, QUEUE_COUNT],
    setup: async page => {
      const panel = await queueAs(page, { hasGamertag: false });
      await expect(panel).toContainText('Set your gamertag');
      return panel;
    },
    close: null,
  },
  {
    name: 'Late League queue, Late League off', role: 'player', path: '/late-league', sheet: 'late-league',
    parts: [HEADING, QUEUE_COUNT],
    setup: async page => {
      const panel = await queueAs(page, { open: false, count: 0 });
      await expect(panel).toContainText('Late League is off here.');
      return panel;
    },
    close: null,
  },
  {
    name: 'Late League queue, failed', role: 'player', path: '/', sheet: 'home',
    parts: [HEADING],
    setup: async page => {
      await page.route('**/api/Queue/Status', route => route.fulfill({ status: 500, body: '' }));
      await open(page, page.url().slice(SITE.length));
      const panel = page.locator('app-late-league-queue');
      await expect(panel).toContainText('The Late League queue could not be loaded.');
      return panel;
    },
    close: null,
  },
  // The Telemetry page (it opens on Overview, or on a trace from a link): its Logs tab, one level only, a log line
  // open, every line of one trace, its Traces tab, the failed ones only, and a span of a trace open
  {
    name: 'telemetry, logs', role: 'admin', path: '/usermanagement/telemetry', sheet: 'telemetry',
    parts: [HEADING, TABS, '.tm-log-line'],
    setup: async page => {
      await telemetryTab(page, 'Logs');
      const line = page.locator('.tm-log-line').first();
      await expect(line).toContainText('the Halo API answered 503');
      return line;
    },
    close: null,
  },
  {
    name: 'telemetry, logs, errors only', role: 'admin', path: '/usermanagement/telemetry', sheet: 'telemetry',
    parts: [HEADING, TABS, '.tm-log-line'],
    setup: async page => {
      await telemetryTab(page, 'Logs');
      const level = page.getByRole('button', { name: 'error', exact: true });
      await level.click();
      await expect(level).toHaveAttribute('aria-pressed', 'true');
      await expect(page.locator('.tm-log-line').first()).toBeVisible();
      return level;
    },
    close: null,
  },
  {
    // Not a dialog: a log line opened on the Logs tab, its properties and links to its trace
    name: 'telemetry, log line', role: 'admin', path: '/usermanagement/telemetry', sheet: 'telemetry',
    parts: [HEADING, TABS, '.tm-detail'],
    setup: openLogLine,
    close: null,
  },
  {
    name: 'telemetry, logs of one trace', role: 'admin', path: '/usermanagement/telemetry', sheet: 'telemetry',
    parts: [HEADING, TABS, '.tm-log-line'],
    setup: async page => {
      const detail = await openLogLine(page);
      await detail.getByRole('button', { name: 'Every line of the trace' }).click();
      const showAll = page.getByRole('button', { name: "Show every trace's lines" });
      await expect(showAll).toBeVisible();
      return showAll;
    },
    close: null,
  },
  {
    name: 'telemetry, traces', role: 'admin', path: '/usermanagement/telemetry', sheet: 'telemetry',
    parts: [HEADING, TABS],
    setup: async page => {
      await telemetryTab(page, 'Traces');
      const table = page.getByRole('region', { name: 'Traces', exact: true });
      await expect(table).toContainText('recurring/historical-refresh');
      return table;
    },
    close: null,
  },
  {
    name: 'telemetry, failed traces only', role: 'admin', path: '/usermanagement/telemetry', sheet: 'telemetry',
    parts: [HEADING, TABS],
    setup: async page => {
      await telemetryTab(page, 'Traces');
      const failedOnly = page.getByRole('button', { name: 'Failed only', exact: true });
      await failedOnly.click();
      await expect(failedOnly).toHaveAttribute('aria-pressed', 'true');
      const table = page.getByRole('region', { name: 'Traces', exact: true });
      await expect(table).toBeVisible();
      return table;
    },
    close: null,
  },
  {
    // Not a dialog: a span opened in a trace's waterfall (its attributes and its exception event)
    name: 'telemetry, trace span', role: 'admin', path: '/usermanagement/telemetry?trace=4bf92f3577b34da6a3ce929d0e0e4736', sheet: 'telemetry',
    parts: [HEADING, TABS, '.tm-detail'],
    setup: async page => {
      await page.locator('.tm-span').first().click();
      const detail = page.locator('.tm-detail');
      await expect(detail).toContainText('HttpRequestException');
      return detail;
    },
    close: null,
  },
  // The gamertag claims page: its Approved and Rejected tabs (it opens on Pending), and the possible duplicates found
  {
    name: 'gamertag claims, approved', role: 'admin', path: '/usermanagement/claims', sheet: 'gamertag-claims',
    parts: [HEADING, TABS],
    setup: page => showClaims(page, 'Approved', 'Old School'),
    close: null,
  },
  {
    name: 'gamertag claims, rejected', role: 'admin', path: '/usermanagement/claims', sheet: 'gamertag-claims',
    parts: [HEADING, TABS],
    setup: page => showClaims(page, 'Rejected', 'That was someone else'),
    close: null,
  },
  {
    name: 'gamertag claims, possible duplicates', role: 'admin', path: '/usermanagement/claims', sheet: 'gamertag-claims',
    parts: [HEADING, TABS],
    setup: async page => {
      await page.getByRole('button', { name: 'Find possible duplicates' }).click();
      // Its table (the section around it is a region of the same name)
      const table = page.locator('.claims-duplicates .hud-table-wrap');
      await expect(table).toContainText('OldSchool #1234');
      return table;
    },
    close: null,
  },
  {
    name: 'database backup, format list', role: 'admin', path: '/databasebackup', sheet: 'database-backup',
    setup: async page => {
      await page.getByRole('combobox', { name: 'Format' }).click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toContainText('PostgreSQL dump');
      return listbox;
    },
  },
  {
    name: 'database backup, server list', role: 'admin', path: '/databasebackup', sheet: 'database-backup',
    setup: async page => {
      await page.getByRole('combobox', { name: 'Server' }).click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toContainText('SQL Server');
      return listbox;
    },
  },
  // The season's schedule, as a commissioner edits it: its time pickers, the recommended times (and applying them,
  // waiting for its Confirm) and the Create Regular Season Matches dialog
  {
    name: 'schedule, editing', role: 'admin', path: '/season/12?tab=schedule', sheet: 'season-schedule',
    parts: [HEADING, TABS, '.sched-week'],
    setup: async page => {
      await page.getByRole('button', { name: 'Show Edit' }).click();
      const recommended = page.getByRole('region', { name: 'Recommended times', exact: true });
      await expect(recommended).toContainText('Sword Swallowers');
      return recommended;
    },
    close: null,
  },
  {
    name: 'schedule, applying the recommended times', role: 'admin', path: '/season/12?tab=schedule', sheet: 'season-schedule',
    parts: [HEADING, TABS, '.sched-week'],
    setup: async page => {
      await page.getByRole('button', { name: 'Show Edit' }).click();
      await page.getByRole('button', { name: 'Apply these times' }).click();
      const confirm = page.locator('app-schedule-list').getByRole('button', { name: 'Confirm' });
      await expect(confirm).toBeVisible();
      return confirm;
    },
    close: null,
  },
  {
    name: 'create matches dialog', role: 'admin', path: '/season/12?tab=schedule', sheet: 'season-schedule',
    setup: async page => {
      await page.getByRole('button', { name: 'Create Regular Season Matches' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      return dialog;
    },
  },
  {
    name: 'schedule, match time picker', role: 'admin', path: '/season/12?tab=schedule', sheet: 'season-schedule',
    setup: async page => {
      await page.getByRole('button', { name: 'Show Edit' }).click();
      await page.locator('.sched-match__form mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'create bracket dialog', role: 'admin', path: '/season/12?tab=bracket', sheet: 'season-bracket',
    setup: async page => {
      await page.getByRole('button', { name: 'Create Bracket' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      return dialog;
    },
  },
  {
    name: 'seeds dialog', role: 'admin', path: '/season/12?tab=bracket', sheet: 'season-bracket',
    setup: async page => {
      await page.getByRole('button', { name: 'Set Seeds' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toContainText('Red Rampage');
      return dialog;
    },
  },
  // The commissioner dashboard: its Overdue Matches tab (it opens on Pending Reschedules), processing a reschedule,
  // and the tooltip of its Discord thread button
  {
    name: 'commissioner dashboard, overdue matches', role: 'admin', path: '/commissioner-dashboard', sheet: 'commissioner-dashboard',
    parts: [HEADING, '.cd-metric'],
    setup: async page => {
      const tab = page.getByRole('tab', { name: /Overdue Matches/ });
      await tab.click();
      await expect(tab).toHaveAttribute('aria-selected', 'true');
      const table = page.getByRole('region', { name: 'Overdue matches', exact: true });
      await expect(table).toContainText('Sticky Situation');
      return table;
    },
    close: null,
  },
  {
    name: 'process reschedule dialog', role: 'admin', path: '/commissioner-dashboard', sheet: 'commissioner-dashboard',
    setup: async page => {
      await page.getByRole('button', { name: 'Process' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      return dialog;
    },
  },
  {
    name: 'reject-only reschedule dialog', role: 'admin', path: '/commissioner-dashboard', sheet: 'commissioner-dashboard',
    setup: async page => {
      await page.getByRole('button', { name: 'Reject' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toContainText('This match already has a result.');
      return dialog;
    },
  },
  {
    name: 'Discord thread tooltip', role: 'admin', path: '/commissioner-dashboard', sheet: 'commissioner-dashboard',
    setup: async page => {
      await page.getByRole('button', { name: 'Create Discord thread' }).hover();
      const tooltip = page.locator('.mat-mdc-tooltip');
      await expect(tooltip).toBeVisible();
      return tooltip;
    },
    close: null, // a tooltip needs no Esc; the next navigation clears it
  },
  // User management: an action's tooltip, the note a password reset link leaves, and a user's lockout picker
  {
    name: 'user management tooltip', role: 'admin', path: '/usermanagement', sheet: 'user-management',
    setup: async page => {
      await page.getByRole('link', { name: 'EDIT', exact: true }).first().hover();
      const tooltip = page.locator('.mat-mdc-tooltip');
      await expect(tooltip).toBeVisible();
      return tooltip;
    },
    close: null,
  },
  {
    name: 'user management, password reset link', role: 'admin', path: '/usermanagement', sheet: 'user-management',
    setup: async page => {
      await page.getByRole('button', { name: /RESET PASSWORD/ }).first().click();
      const note = page.locator('mat-snack-bar-container');
      await expect(note).toContainText('Password reset link');
      return note;
    },
    close: null,
  },
  {
    name: 'edit user, lockout picker', role: 'admin', path: '/usermanagement/edituser/2', sheet: 'user-management',
    setup: async page => {
      await page.locator('mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'season editor, date picker', role: 'admin', path: '/seasonEdit/12', sheet: 'season-editor',
    setup: async page => {
      await page.locator('mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'season editor, copy-from list', role: 'admin', path: '/seasonEdit/12', sheet: 'season-editor',
    setup: async page => {
      await page.getByRole('combobox', { name: 'Copy from' }).click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toContainText('Winter Campaign');
      return listbox;
    },
  },
  {
    name: 'availability, time picker', role: 'admin', path: '/seasonAvailability/12', sheet: 'season-editor',
    setup: async page => {
      await page.locator('mtx-datetimepicker-toggle button').first().click();
      const picker = page.locator('mtx-datetimepicker-content');
      await expect(picker).toBeVisible();
      return picker;
    },
  },
  {
    name: 'availability, day list', role: 'admin', path: '/seasonAvailability/12', sheet: 'season-editor',
    setup: async page => {
      await page.getByRole('combobox').first().click();
      const listbox = page.getByRole('listbox');
      await expect(listbox).toBeVisible();
      return listbox;
    },
  },
  // The historical import page: a job's panel (a guarded import with seasons to decide on), its failures and warnings
  // opened, and a VOD from the review list being linked to a season
  {
    // Not a dialog: the job panel the page opens above its jobs list
    name: 'historical import, job details', role: 'admin', path: '/usermanagement/historicalimport', sheet: 'historical-import',
    parts: [HEADING, '.hi-job'],
    setup: async page => {
      await page.getByRole('button', { name: 'Show job 41' }).click();
      const panel = page.locator('.hi-job');
      await expect(panel).toContainText('Needs review');
      return panel;
    },
    close: null,
  },
  {
    name: 'historical import, failures and warnings', role: 'admin', path: '/usermanagement/historicalimport', sheet: 'historical-import',
    parts: [HEADING, '.hi-job'],
    setup: async page => {
      await page.getByRole('button', { name: 'Show job 41' }).click();
      const details = page.locator('.hi-details');
      await details.locator('summary').click();
      await expect(details).toHaveAttribute('open', '');
      await expect(details).toContainText('stopped for review');
      return details;
    },
    close: null,
  },
  {
    name: 'historical import, linking a VOD', role: 'admin', path: '/usermanagement/historicalimport', sheet: 'historical-import',
    parts: [HEADING, '.vr-editor'],
    setup: async page => {
      await page.getByRole('button', { name: /^Link AGLA Playoffs/ }).click();
      const editor = page.locator('.vr-editor');
      await expect(editor.locator('.vr-team').first()).toBeVisible();
      return editor;
    },
    close: null,
  },
  // The Jobs page: the panel it opens under its list, of a job that runs and of one paused
  {
    name: 'jobs, job details', role: 'admin', path: '/usermanagement/jobs', sheet: 'jobs',
    parts: [HEADING, '.jb-detail'],
    setup: async page => {
      await page.getByRole('button', { name: 'Details of historical-refresh' }).click();
      const panel = page.locator('.jb-detail');
      await expect(panel).toContainText('Recent runs');
      return panel;
    },
    close: null,
  },
  {
    name: 'jobs, a paused job', role: 'admin', path: '/usermanagement/jobs', sheet: 'jobs',
    parts: [HEADING, '.jb-detail'],
    setup: async page => {
      await page.getByRole('button', { name: 'Details of xbox-placeholders' }).click();
      const panel = page.locator('.jb-detail');
      await expect(panel).toContainText('Paused on the Jobs page');
      return panel;
    },
    close: null,
  },
  {
    name: 'reschedule date picker', role: 'admin', path: '/reschedule-request/31', sheet: 'reschedule-request',
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
