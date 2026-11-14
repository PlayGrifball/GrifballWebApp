import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import type { SeasonSummaryDto } from '@api/models';
import { HomeService } from '@api/services/home.service';
import { SeasonService } from '@api/services/season.service';
import { SeasonEvent } from '@shared/seasonEvents';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import { SeasonSummaryService } from './seasonSummary';

/**
 * The season page's season: the route's, or the current one when it names none (0), with its summary, and the current
 * and upcoming events (for Sign up). One per page (the component provides it).
 */
@Injectable()
export class SeasonStore {
  private readonly seasons = inject(SeasonService);
  private readonly home = inject(HomeService);
  private readonly summaries = inject(SeasonSummaryService);

  /** The route's season (0: the current season): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** The season shown: the route's, or the current season's ID once known. */
  private readonly _shownSeasonID = signal(0);
  readonly shownSeasonID = this._shownSeasonID.asReadonly();
  private readonly _seasonName = signal<string | null>('Season');
  readonly seasonName = this._seasonName.asReadonly();
  /** What the season is and has: its header, champions, and which sections have nothing to show. */
  private readonly _summary = signal<SeasonSummaryDto | null>(null);
  readonly summary = this._summary.asReadonly();
  /** The name is in (or failed to load). */
  private readonly _nameLoaded = signal(false);
  readonly nameLoaded = this._nameLoaded.asReadonly();
  /** The current and upcoming signups, drafts and seasons (empty until they load, or if they fail). */
  private readonly _events = signal<SeasonEvent[]>([]);
  readonly events = this._events.asReadonly();
  private readonly _eventsLoaded = signal(false);
  readonly eventsLoaded = this._eventsLoaded.asReadonly();

  constructor() {
    effect(() => {
      const routeSeasonID = this.seasonID.value();
      if (routeSeasonID !== undefined)
        untracked(() => this.show(routeSeasonID));
    });
  }

  loadEvents(): void {
    this.home.currentAndFutureEvents().subscribe({
      next: events => {
        this._events.set(events ?? []);
        this._eventsLoaded.set(true);
      },
      error: () => this._eventsLoaded.set(true),
    });
  }

  private show(routeSeasonID: number): void {
    // Nothing of the previous season stays: its name and summary. The page's sections get the new ID and load it.
    this._shownSeasonID.set(routeSeasonID);
    this._summary.set(null);
    this._seasonName.set('Season');
    this._nameLoaded.set(false);
    if (routeSeasonID !== 0) {
      this.loadSummary();
      return;
    }
    this.seasons.getCurrentSeasonId()
      .pipe(whileCurrent(routeSeasonID, this.seasonID.value))
      .subscribe({
        next: seasonID => {
          this._shownSeasonID.set(seasonID);
          this.loadSummary();
        },
      });
  }

  /** Loads the summary of the season shown again (its bracket was made again or reseeded, which clears its champion). */
  loadSummary(): void {
    const seasonID = this.shownSeasonID();
    this.summaries.get(seasonID)
      .pipe(whileCurrent(seasonID, this.shownSeasonID))
      .subscribe({
        next: summary => {
          this._summary.set(summary);
          this._seasonName.set(summary.seasonName);
          this._nameLoaded.set(true);
        },
        error: () => this._nameLoaded.set(true),
      });
  }
}
