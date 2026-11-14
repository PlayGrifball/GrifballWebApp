import { inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { QuickSearchPlayerDto } from '@api/models';
import { LeaderboardsService } from '@api/services/leaderboards.service';
import { QUICK_SEARCH_MIN_LENGTH } from '@core/layout/quickSearch/quickSearch.component';
import { catchError, map, of, Subject, switchMap, timer } from 'rxjs';

/** How long typing pauses before the players are searched. */
export const SEARCH_DEBOUNCE_MS = 250;

/**
 * The player search's suggestions: the players found for the text typed, once typing pauses. One per search box (the
 * component provides it).
 */
@Injectable()
export class PlayerSearchStore {
  private readonly api = inject(LeaderboardsService);

  /** The players found; a new list (empty too) each time an answer lands or the search is dropped. */
  private readonly _results = signal<QuickSearchPlayerDto[]>([]);
  readonly results = this._results.asReadonly();
  /** The text the results are for. */
  private readonly _resultsFor = signal('');
  readonly resultsFor = this._resultsFor.asReadonly();

  /**
   * Each new text cancels the pending search and any request in flight (switchMap), so a stale answer never lands after
   * a newer one, or after the list was closed. Short text (and a cancel: '') empties the results at once.
   */
  private readonly texts = new Subject<string>();

  constructor() {
    this.texts.pipe(
      switchMap(text => text.length < QUICK_SEARCH_MIN_LENGTH ? of({ text, results: [] }) : timer(SEARCH_DEBOUNCE_MS).pipe(
        switchMap(() => this.api.searchPlayers({ query: text })),
        catchError(() => of([])),
        map(results => ({ text, results })),
      )),
      takeUntilDestroyed(),
    ).subscribe(({ text, results }) => {
      this._resultsFor.set(text);
      this._results.set([...results]);
    });
  }

  /** Searches the players for the text, once typing pauses. */
  search(text: string): void {
    this.texts.next(text);
  }

  /** Drops the pending search and empties the results. */
  cancel(): void {
    this.texts.next('');
  }
}
