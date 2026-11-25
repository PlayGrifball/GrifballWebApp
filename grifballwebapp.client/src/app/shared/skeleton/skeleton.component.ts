import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** The rough shape of what is loading: lines of text, a table, a grid of cards, or one block (a chart, a bracket). */
export type SkeletonShape = 'lines' | 'table' | 'cards' | 'block';

/**
 * Holds the place of something still loading, in about its shape and size, so it doesn't push the page around when it
 * arrives (layout shift). A page or part shows it in its load's `@else` (or `@defer`'s `@placeholder`), never next
 * to what it stands for. Screen readers read "Loading {label}" where it is; the shapes are hidden from them. It is
 * not a live region (role=status): a page loading several parts would announce each.
 */
@Component({
  selector: 'app-skeleton',
  template: `
    <span class="skeleton__label">Loading {{label()}}&hellip;</span>
    @switch (shape()) {
      @case ('table') {
        <div class="skeleton__table" aria-hidden="true" [style.--skeleton-columns]="columns()">
          @if (header()) {
            @for (cell of headerCells(); track $index) {
              <span class="skeleton__bar skeleton__bar--head"></span>
            }
          }
          @for (cell of bodyCells(); track $index) {
            <span class="skeleton__bar"></span>
          }
        </div>
      }
      @case ('cards') {
        <div class="skeleton__cards" aria-hidden="true" [style.--skeleton-card-min]="cardWidth()">
          @for (card of items(); track $index) {
            <span class="skeleton__bar skeleton__card" [style.height]="height() ?? '160px'"></span>
          }
        </div>
      }
      @case ('block') {
        <span class="skeleton__bar skeleton__block" aria-hidden="true" [style.height]="height() ?? '240px'"></span>
      }
      @default {
        <div class="skeleton__lines" aria-hidden="true">
          @for (line of items(); track $index) {
            <span class="skeleton__bar skeleton__line"></span>
          }
        </div>
      }
    }
  `,
  styles: `
    :host { display: block; margin: 8px 0; }
    .skeleton__label {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    .skeleton__bar {
      display: block;
      border-radius: 2px;
      background: linear-gradient(90deg, var(--hud-panel-faint) 25%, var(--hud-panel-soft) 50%, var(--hud-panel-faint) 75%) 0 0 / 300% 100%;
      border: 1px solid var(--hud-hairline);
      animation: skeleton-shimmer 1.6s ease-in-out infinite;
    }
    .skeleton__lines { display: grid; gap: 10px; }
    .skeleton__line { height: 14px; }
    .skeleton__line:last-child:not(:first-child) { width: 60%; }
    .skeleton__table {
      display: grid;
      grid-template-columns: repeat(var(--skeleton-columns), minmax(0, 1fr));
      gap: 1px 12px;
    }
    .skeleton__table .skeleton__bar { height: 47px; border-width: 0 0 1px; border-radius: 0; }
    .skeleton__table .skeleton__bar--head { height: 55px; }
    .skeleton__cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(100%, var(--skeleton-card-min)), 1fr));
      gap: 16px;
    }
    .skeleton__block { width: 100%; }
    @keyframes skeleton-shimmer {
      from { background-position: 100% 0; }
      to { background-position: 0 0; }
    }
    @media (prefers-reduced-motion: reduce) {
      .skeleton__bar { animation: none; }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkeletonComponent {
  /** What is loading, for screen readers: "Loading {label}". */
  readonly label = input.required<string>();
  readonly shape = input<SkeletonShape>('lines');
  /** Lines of text, table rows (under the header), or cards. */
  readonly count = input(3);
  /** A table's columns. */
  readonly columns = input(4);
  /** Whether a table has its header row: not when the skeleton goes under a real table's header. */
  readonly header = input(true);
  /** A card's or block's height (CSS), when the default (160px a card, 240px a block) is not about right. */
  readonly height = input<string>();
  /** The narrowest a card gets before the grid wraps (CSS). */
  readonly cardWidth = input('240px');

  protected readonly items = computed(() => Array.from({ length: Math.max(0, this.count()) }));
  protected readonly headerCells = computed(() => Array.from({ length: Math.max(1, this.columns()) }));
  protected readonly bodyCells = computed(() => Array.from({ length: Math.max(1, this.columns()) * Math.max(0, this.count()) }));
}
