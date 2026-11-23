import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Says why a section has nothing to show (signups, a schedule, matches or match stats a season does not have), in its
 * place, rather than leaving an empty table. Links the source spreadsheet when one is given.
 */
@Component({
  selector: 'app-data-notice',
  template: `
    <p class="data-notice" role="note">
      <span>{{message()}}</span>
      @if (sourceUrl(); as url) {
        <a class="data-notice__link" [href]="url" target="_blank" rel="noopener noreferrer">Source spreadsheet</a>
      }
    </p>
  `,
  styles: `
    .data-notice {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 12px;
      margin: 8px 0;
      padding: 12px 14px;
      background: var(--hud-well);
      border: 1px dashed var(--hud-border);
      border-left: 3px solid var(--hud-accent);
      color: var(--hud-text);
      font-size: 14px;
      line-height: 1.5;
    }
    .data-notice__link { display: inline-flex; align-items: center; min-height: 44px; color: var(--hud-accent); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DataNoticeComponent {
  readonly message = input.required<string>();
  readonly sourceUrl = input<string | null | undefined>();
}

/** A spreadsheet that feeds a season: its link and a name for it. */
export interface SourceLink {
  url: string;
  label: string;
}

/**
 * The page-level banner of a season backfilled from the historical archive, with its source spreadsheets: each one labelled
 * when the season has several (stats, schedule, champions), else the one "Source spreadsheet".
 */
@Component({
  selector: 'app-backfilled-banner',
  template: `
    <p class="backfilled" role="note">
      <span class="backfilled__label">Archived from historical records</span>
      @if (eraLabel(); as era) {
        <span aria-hidden="true">·</span>
        <span class="backfilled__era">{{era}}</span>
      }
      @for (link of links(); track link.url) {
        <span aria-hidden="true">·</span>
        <a class="backfilled__link" [href]="link.url" target="_blank" rel="noopener noreferrer">{{link.label}}</a>
      }
    </p>
  `,
  styles: `
    .backfilled {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin: 0 0 12px;
      padding: 0 14px;
      min-height: 44px;
      background: var(--hud-well);
      border: 1px solid var(--hud-border);
      border-left: 3px solid var(--hud-gold);
      color: var(--hud-text);
      font: 13px var(--hud-font-mono);
      letter-spacing: 0.04em;
    }
    .backfilled__label { text-transform: uppercase; color: var(--hud-gold); }
    .backfilled__era { color: var(--hud-muted-2); }
    .backfilled__link { display: inline-flex; align-items: center; min-height: 44px; color: var(--hud-accent); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BackfilledBannerComponent {
  /** The season's spreadsheets, the primary first. */
  readonly sources = input<readonly SourceLink[] | null | undefined>();
  /** The season's one source link, when its spreadsheets are not listed. */
  readonly sourceUrl = input<string | null | undefined>();
  readonly eraLabel = input<string | null | undefined>();

  protected readonly links = computed<SourceLink[]>(() => {
    const sources = this.sources() ?? [];
    if (sources.length > 1) {
      return [...sources];
    }
    const url = sources[0]?.url ?? this.sourceUrl();
    return url ? [{ url, label: 'Source spreadsheet' }] : [];
  });
}
