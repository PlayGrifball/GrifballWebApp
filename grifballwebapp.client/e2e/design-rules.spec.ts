import { expect, test, type Page } from '@playwright/test';
import { closeOverlay, mockBackend, open, OVERLAYS, ROUTES } from './testPages.mts';

// Measures the rendered UI against a handful of plain rules that axe (a11y.spec.ts) does not check,
// at all six screen sizes (e2e/layout.spec.ts's sizes), on every page and every open dialog, menu
// and picker (e2e/testPages.mts, shared with a11y.spec.ts):
//  - visible text is at least 12px
//  - the value text in an input, a mat-select or a textarea, and a button's label, starts at least
//    8px from its box's border (caught the theme dialog's labels sitting flush against the outline)
//  - interactive targets are at least 44x44px, at the two portrait sizes (WCAG 2.5.8, with the
//    inline-link-in-running-text exception)
//  - no text is clipped (scrollWidth/Height > clientWidth/Height on an overflow:hidden/clip element)
//    unless it is an intentional ellipsis with a title or aria-label carrying the full text
//  - nothing spills out of the page horizontally, or out of an open overlay's own box
//  - no two sibling interactive elements, or sibling text boxes, overlap
//
// Third-party markup (brackets-viewer, the date/time and colour pickers) is narrowly excluded where
// noted below, the same way a11y.spec.ts excludes it from specific axe rules.

const SIZES = [
  ['desktop', 1920, 1080],
  ['laptop', 1280, 720],
  ['tablet-landscape', 1024, 768],
  ['tablet-portrait', 768, 1024],
  ['phone-landscape', 820, 300],
  ['phone-portrait', 360, 640],
] as const;

// An overlay fades/scales in; measured mid-animation it is the wrong size (as a11y.spec.ts notes
// for the same reason).
const SETTLE_MS = 300;

// WCAG 2.5.8's target-size check only makes sense at the narrow sizes: at a desktop width nothing
// in this app is actually cramped, and running it everywhere would mostly flag buttons that have
// plenty of room on their own line.
const TAP_TARGET_SIZES: readonly string[] = ['tablet-portrait', 'phone-portrait'];

/**
 * Runs the checks against the current page/viewport; see the module comment for what each means.
 * Runs inside the page (page.evaluate): it only sees its own arguments and globals, not this
 * file's module scope, so everything it needs is declared inside it.
 */
function runDesignRules(checkTapTargets: boolean): string[] {
  const results: string[] = [];
  // Markup the app does not write, or that is deliberately clipped/overlapping as decoration (the
  // chamfered panels' corner cut, drawn with a background gradient, not clipped text); and the
  // availability grid (app-availability-table), a spreadsheet of one checkbox per day/time with no
  // room for a 44px target or 8px of padding around its toggle-a-row/column buttons at every size -
  // the WCAG 2.5.8 exception for a dense, essential data-entry grid.
  const EXCLUDED = [
    '.brackets-viewer',
    'mat-datepicker-content', 'mtx-datetimepicker-content', 'mtx-colorpicker-content',
    'app-availability-table',
  ].map(s => `${s}, ${s} *`).join(', ');
  const excluded = document.querySelectorAll(EXCLUDED);
  const isExcluded = (el: Element) => [...excluded].some(e => e === el || e.contains(el));

  const shortSelector = (el: Element): string => {
    if (el.id)
      return '#' + el.id;
    const cls = typeof el.className === 'string' && el.className.trim() ? '.' + el.className.trim().split(/\s+/).slice(0, 2).join('.') : '';
    return el.tagName.toLowerCase() + cls;
  };

  const isVisible = (el: Element): boolean => {
    const style = getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden' || Number(style.opacity) === 0)
      return false;
    if (el.closest('[aria-hidden="true"]'))
      return false;
    const rect = el.getBoundingClientRect();
    // Zero-size, or the classic visually-hidden (sr-only) 1x1 clipped box
    return rect.width > 1 && rect.height > 1;
  };

  const hasDirectText = (el: Element): boolean =>
    [...el.childNodes].some(n => n.nodeType === Node.TEXT_NODE && (n.textContent ?? '').trim() !== '');

  // A Material outlined field's visible box, and its real tap target, is its wrapper: the control
  // itself (an <input>, or a mat-select's small trigger line) is only as tall as one line of text.
  const boxFor = (el: Element): Element => el.closest('.mat-mdc-text-field-wrapper') ?? el;

  // 1. Font size
  for (const el of document.querySelectorAll('*')) {
    if (isExcluded(el) || !isVisible(el) || !hasDirectText(el))
      continue;
    const fontSize = parseFloat(getComputedStyle(el).fontSize);
    if (fontSize < 11.9)
      results.push(`font-size: ${shortSelector(el)} is ${fontSize.toFixed(1)}px`);
  }

  // 2. Form field / button text inset from its box's border. For an input, a textarea or a
  // mat-select's value (no visible border of their own), the box is the Material field's outlined
  // wrapper; for a button, the button is its own box.
  // "Buttons" means actual buttons and button-styled links, not every element with role=button
  // (e.g. mat-sort-header's, which lays its text out with flex gap, not padding).
  const insetTargets = [
    ...document.querySelectorAll('input:not([type=hidden]):not([type=checkbox]):not([type=radio]):not([type=range])'),
    ...document.querySelectorAll('textarea, .mat-mdc-select-value'),
    ...document.querySelectorAll('button, a.hud-btn'),
  ];
  for (const el of insetTargets) {
    if (isExcluded(el) || !isVisible(el))
      continue;
    const text = el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement
      ? (el.value || el.placeholder || '').trim()
      : (el.textContent ?? '').trim();
    if (!text)
      continue;
    const box = boxFor(el);
    const boxRect = box.getBoundingClientRect();
    const elRect = el.getBoundingClientRect();
    const cs = getComputedStyle(el);
    const insetLeft = (elRect.left - boxRect.left) + parseFloat(cs.paddingLeft || '0');
    const insetRight = (boxRect.right - elRect.right) + parseFloat(cs.paddingRight || '0');
    // A button with a leading or trailing icon that is decorative (aria-hidden="true") or not
    // currently shown (e.g. the header's user chip hides the name and shows only the avatar on a
    // phone) is allowed to sit close to that side: the box-model approximation above can't tell
    // "icon" from "text", and an icon sitting near its own edge is normal.
    const isIconEdge = (child: Element | null) => child != null && (child.getAttribute('aria-hidden') === 'true' || !isVisible(child));
    const firstHidden = isIconEdge(el.firstElementChild);
    const lastHidden = isIconEdge(el.lastElementChild);
    if (insetLeft < 7.5 && !firstHidden)
      results.push(`field-inset: ${shortSelector(el)} text starts ${insetLeft.toFixed(1)}px from the left border`);
    if (insetRight < 7.5 && !lastHidden)
      results.push(`field-inset: ${shortSelector(el)} text starts ${insetRight.toFixed(1)}px from the right border`);
  }

  // 3. Tap target size (only at the two portrait sizes the caller asks for)
  if (checkTapTargets) {
    const interactive = document.querySelectorAll([
      'a[href]', 'button', 'input:not([type=hidden])', 'select', 'textarea',
      '[role=button]', '[role=link]', '[role=checkbox]', '[role=radio]', '[role=tab]', '[role=menuitem]',
      'mat-checkbox', 'mat-radio-button', 'mat-slide-toggle',
      '[tabindex]:not([tabindex="-1"])',
    ].join(', '));
    for (const el of interactive) {
      if (isExcluded(el) || !isVisible(el))
        continue;
      // WCAG 2.5.8's exception: a link inline in a run of text, not a styled nav/button link
      if (el.tagName === 'A' && getComputedStyle(el).display === 'inline')
        continue;
      // A checkbox, radio button or slide toggle's own clickable piece inside it (its ripple
      // target, or - for a slide toggle - an inner <button>) is not a second, separate target:
      // the host (matched on its own below, and sized by the rule above) is what a user taps.
      const control = el.closest('mat-checkbox, mat-radio-button, mat-slide-toggle');
      if (control && control !== el)
        continue;
      const rect = boxFor(el).getBoundingClientRect();
      if (rect.width < 43.5 || rect.height < 43.5)
        results.push(`tap-target: ${shortSelector(el)} is ${rect.width.toFixed(0)}x${rect.height.toFixed(0)}px`);
    }
  }

  // 4. Clipped text. Material's own mat-cell/mat-header-cell CSS truncates every table column with
  // an ellipsis by default (not something a page's own layout controls); the app's own long text in
  // a cell (e.g. commissioner-dashboard's reason-cell) carries its own title, which this still checks.
  for (const el of document.querySelectorAll('*')) {
    if (isExcluded(el) || !isVisible(el) || !hasDirectText(el) || el.matches('.mat-mdc-cell, .mat-mdc-header-cell'))
      continue;
    const style = getComputedStyle(el);
    const clipsX = (style.overflowX === 'hidden' || style.overflowX === 'clip') && el.scrollWidth > el.clientWidth + 1;
    const clipsY = (style.overflowY === 'hidden' || style.overflowY === 'clip') && el.scrollHeight > el.clientHeight + 1;
    if (!clipsX && !clipsY)
      continue;
    const ellipsis = style.textOverflow === 'ellipsis' && (el.hasAttribute('title') || el.hasAttribute('aria-label'));
    if (!ellipsis)
      results.push(`clipped-text: ${shortSelector(el)} content does not fit (${clipsX ? 'horizontally' : 'vertically'}) and is not an ellipsis with a title/aria-label`);
  }

  // 5. Spilling out of the page, or (within an open overlay) out of the overlay's own box
  if (document.documentElement.scrollWidth > window.innerWidth + 1)
    results.push(`overflow: the page is ${document.documentElement.scrollWidth}px wide, wider than the ${window.innerWidth}px viewport`);
  const overlay = document.querySelector('[role=dialog], [role=menu], [role=listbox], .cdk-overlay-pane, mat-sidenav.mat-drawer-opened');
  if (overlay) {
    const overlayRect = overlay.getBoundingClientRect();
    for (const el of overlay.querySelectorAll('*')) {
      if (isExcluded(el) || !isVisible(el) || el.closest('.cdk-overlay-pane') !== overlay && !overlay.contains(el))
        continue;
      // A nested overlay (e.g. a select panel opened from inside a dialog) has its own box
      if (el.closest('[role=dialog], [role=menu], [role=listbox], .cdk-overlay-pane') !== overlay)
        continue;
      const rect = el.getBoundingClientRect();
      if (rect.width === 0 && rect.height === 0)
        continue;
      if (rect.left < overlayRect.left - 1 || rect.right > overlayRect.right + 1)
        results.push(`overlay-overflow: ${shortSelector(el)} extends past ${shortSelector(overlay)}'s own box`);
    }
  }

  // 6. Overlapping siblings (interactive elements, or boxes with their own direct text)
  const intersects = (a: DOMRect, b: DOMRect, tolerance: number): boolean =>
    a.left < b.right - tolerance && a.right > b.left + tolerance && a.top < b.bottom - tolerance && a.bottom > b.top + tolerance;
  for (const parent of document.querySelectorAll('*')) {
    if (isExcluded(parent))
      continue;
    const candidates = [...parent.children].filter(el =>
      isVisible(el) && !isExcluded(el) && (hasDirectText(el) || el.matches('a[href], button, input, select, textarea, [role=button], [role=link], [tabindex]')));
    for (let i = 0; i < candidates.length; i++) {
      for (let j = i + 1; j < candidates.length; j++) {
        const a = candidates[i], b = candidates[j];
        if (a.contains(b) || b.contains(a))
          continue;
        if (intersects(a.getBoundingClientRect(), b.getBoundingClientRect(), 2))
          results.push(`overlap: ${shortSelector(a)} and ${shortSelector(b)} (siblings under ${shortSelector(parent)}) overlap`);
      }
    }
  }

  return results;
}

for (const { role, label, paths } of ROUTES) {
  test.describe(label, () => {
    for (const path of paths) {
      test(`${path} follows the design rules`, async ({ page }) => {
        await mockBackend(page, role);
        const problems: string[] = [];
        for (const [device, width, height] of SIZES) {
          await page.setViewportSize({ width, height });
          await open(page, path);
          await page.waitForTimeout(SETTLE_MS);
          const found = await page.evaluate(runDesignRules, TAP_TARGET_SIZES.includes(device));
          problems.push(...found.map(p => `${device} ${path}: ${p}`));
        }
        expect(problems).toEqual([]);
      });
    }
  });
}

test.describe('open', () => {
  for (const overlay of OVERLAYS) {
    test(`${overlay.name} follows the design rules`, async ({ page }) => {
      await mockBackend(page, overlay.role);
      const problems: string[] = [];
      for (const [device, width, height] of SIZES) {
        await page.setViewportSize({ width, height });
        await open(page, overlay.path);
        const opened = await overlay.setup(page);
        if (!opened)
          continue;
        await page.waitForTimeout(SETTLE_MS);
        const found = await page.evaluate(runDesignRules, TAP_TARGET_SIZES.includes(device));
        problems.push(...found.map(p => `${device} ${overlay.name}: ${p}`));
        await closeOverlay(page, overlay, opened);
      }
      expect(problems).toEqual([]);
    });
  }
});
