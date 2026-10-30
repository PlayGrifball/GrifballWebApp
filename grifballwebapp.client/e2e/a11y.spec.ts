import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { closeOverlay, mockBackend, open, OVERLAYS, ROUTES } from './testPages.mts';

// Accessibility, checked by axe-core (https://github.com/dequelabs/axe-core) against WCAG 2.x A and
// AA (and 2.2 AA): every page at a phone and a desktop size, as a visitor and signed in as a
// commissioner and admin ("Commander Grif" in e2e/mockApi.mts, as Lighthouse signs in:
// lighthouserc.cjs), and every dialog, menu and picker in its open state (e2e/testPages.mts, shared
// with e2e/design-rules.spec.ts), which Lighthouse never sees. A serious or critical violation fails
// the test; the summary lists each one by size, page, rule and element.
//
// The pages are rendered on the server (localhost, e2e/serve.mjs) and taken over by the browser,
// with the API mocked here from e2e/mockApi.mts, as in the layout test (layout.spec.ts).

const SIZES = [
  ['phone', 360, 640],
  ['desktop', 1280, 720],
] as const;

const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

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

for (const { role, label, paths } of ROUTES) {
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

// The overlays (e2e/testPages.mts), each scanned while open. The page behind is scanned too (the
// overlay hides it from assistive technology while it is open, which axe takes into account).
test.describe('open', () => {
  for (const overlay of OVERLAYS) {
    test(overlay.name, async ({ page }) => {
      await mockBackend(page, overlay.role);
      const problems: string[] = [];
      for (const [device, width, height] of SIZES) {
        await page.setViewportSize({ width, height });
        await open(page, overlay.path);
        const opened = await overlay.setup(page);
        if (!opened)
          continue; // Does not apply at this size (e.g. the nav drawer button, on desktop)
        problems.push(...await scan(page, `${device} ${overlay.name}`));
        await closeOverlay(page, overlay, opened);
      }
      expect(problems).toEqual([]);
    });
  }
});
