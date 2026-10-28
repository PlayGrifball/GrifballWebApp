import { test as base, expect, type Browser, type BrowserContext, type Locator, type Page } from '@playwright/test';
import { Api } from './api';
import { smokeBaseUrl, smokeUsers, type Credentials } from './env';

/**
 * Browser console errors a preview produces on its own, that say nothing about the change under
 * test. Anything else logged as an error (or thrown uncaught) fails the test.
 */
export const KNOWN_PREVIEW_NOISE: { pattern: RegExp; why: string }[] = [
  {
    // Previews have no Halo Infinite credentials, so the season match page's possible-matches
    // lookup fails with 500 "ClientId is null or empty" (and the browser logs the failed request).
    pattern: /GetPossibleMatches|ClientId is null or empty/,
    why: 'no Halo Infinite credentials in previews',
  },
  {
    // The Teams SignalR hub's first connection attempt is sometimes cut off while negotiating and
    // the client starts over; the retry connects. Logged by @microsoft/signalr as an error.
    pattern: /stopped during negotiation|Failed to start the connection/,
    why: 'SignalR first-connect negotiation restart',
  },
];

export interface ConsoleErrors {
  /** Every error logged or thrown on the page that isn't known noise or allowed by the test. */
  unexpected(): string[];
  /** Allows more messages for this test only, e.g. a 404 the test provokes on purpose. */
  allow(pattern: RegExp): void;
}

export function watchConsole(page: Page): ConsoleErrors {
  const errors: string[] = [];
  const allowed = KNOWN_PREVIEW_NOISE.map(n => n.pattern);
  page.on('console', msg => {
    if (msg.type() === 'error') {
      const where = msg.location().url ? ` (${msg.location().url})` : '';
      errors.push(msg.text() + where);
    }
  });
  page.on('pageerror', err => errors.push(`uncaught: ${err.message}`));
  return {
    unexpected: () => errors.filter(e => !allowed.some(p => p.test(e))),
    allow: pattern => allowed.push(pattern),
  };
}

/** Signs in through the login page, as a person would. */
export async function login(page: Page, user: Credentials): Promise<void> {
  await page.goto('/login');
  await page.locator('input[name="username"]').fill(user.username);
  await page.locator('input[name="password"]').fill(user.password);
  await page.getByRole('button', { name: 'Submit' }).click();
  // The toolbar greets the signed-in user once the MetaInfo round trip is done.
  await expect(page.getByText(`Hello ${user.username}`).first()).toBeVisible();
}

/**
 * Real pointer drag: press on the source, move in small steps, release over the target (its
 * centre, or `at` from its top-left corner). ngx-drag-drop only accepts drags it started itself,
 * from a native dragstart; synthetic DragEvents are ignored as "external" drags.
 */
export async function drag(source: Locator, target: Locator, at?: { x: number; y: number }): Promise<void> {
  const page = source.page();
  await source.scrollIntoViewIfNeeded();
  const from = await source.boundingBox();
  if (!from) throw new Error(`drag: source not visible: ${source}`);
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2 + 5, from.y + from.height / 2 + 5, { steps: 5 });
  const to = await target.boundingBox();
  if (!to) throw new Error(`drag: target not visible: ${target}`);
  await page.mouse.move(to.x + (at?.x ?? to.width / 2), to.y + (at?.y ?? to.height / 2), { steps: 10 });
  await page.mouse.up();
}

interface Fixtures {
  /** Console errors of the test's `page`, checked when the test ends. */
  consoleErrors: ConsoleErrors;
  /** `page`, already signed in as the first smoke user. */
  adminPage: Page;
}

interface WorkerFixtures {
  baseUrl: string;
  /** The two smoke users. */
  smoke: [Credentials, Credentials];
  /** The API as the first smoke user, shared by the worker. */
  admin: Api;
}

export const test = base.extend<Fixtures, WorkerFixtures>({
  baseUrl: [async ({}, use) => use(smokeBaseUrl()), { scope: 'worker' }],
  smoke: [async ({}, use) => use(smokeUsers()), { scope: 'worker' }],
  admin: [async ({ baseUrl, smoke }, use) => {
    const api = await Api.login(baseUrl, smoke[0]);
    await use(api);
    await api.dispose();
  }, { scope: 'worker' }],

  consoleErrors: async ({ page }, use) => {
    const errors = watchConsole(page);
    await use(errors);
    expect(errors.unexpected(), 'unexpected browser console errors').toEqual([]);
  },
  adminPage: async ({ page, smoke, consoleErrors }, use) => {
    void consoleErrors; // Watching before the first navigation.
    await login(page, smoke[0]);
    await use(page);
  },
});

/** A second signed-in browser (its own context, so its own session and SignalR connection). */
export async function secondBrowser(browser: Browser, user: Credentials):
  Promise<{ context: BrowserContext; page: Page; consoleErrors: ConsoleErrors }> {
  const context = await browser.newContext();
  const page = await context.newPage();
  const consoleErrors = watchConsole(page);
  await login(page, user);
  return { context, page, consoleErrors };
}

export { expect };
