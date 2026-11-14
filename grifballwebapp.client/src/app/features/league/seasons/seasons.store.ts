import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import type { SeasonListItemDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { Subscription } from 'rxjs';
import type { SeasonListFilter } from '../api/seasons';

/** A filter's choice: a league or game, by ID. */
export interface Option { id: number; name: string; }

/** The most seasons the list is asked for at once: every season there is. */
export const SEASONS_PAGE_SIZE = 250;

/** Every season so far, filtered, and the leagues, games and years to filter by. One per page (the component provides it). */
@Injectable()
export class SeasonsStore {
  private readonly api = inject(SeasonService);
  private request?: Subscription;

  private readonly _seasons = signal<SeasonListItemDto[]>([]);
  readonly seasons = this._seasons.asReadonly();
  private readonly _loaded = signal(false);
  readonly loaded = this._loaded.asReadonly();
  /** The leagues, games and years there are seasons of (from the unfiltered list); the latest year first. */
  private readonly _leagues = signal<Option[]>([]);
  readonly leagues = this._leagues.asReadonly();
  private readonly _games = signal<Option[]>([]);
  readonly games = this._games.asReadonly();
  private readonly _years = signal<number[]>([]);
  readonly years = this._years.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  /** Loads the seasons the filter picks; `options`: they are every season, so the choices come from them too. */
  load(filter: SeasonListFilter, options: boolean): void {
    // Only the latest filters' list is shown: an older answer arriving late is dropped
    this.request?.unsubscribe();
    this._loaded.set(false);
    this.request = this.api.getSeasonList(listParams(filter)).subscribe({
      next: page => {
        this._seasons.set(page.results);
        if (options)
          this.setOptions(page.results);
        this._loaded.set(true);
      },
      error: () => this._loaded.set(true),
    });
  }

  /** Loads the choices from every season, for a list opened already filtered. */
  loadOptions(): void {
    this.api.getSeasonList(listParams({ leagueID: null, gameVersionID: null, format: null, year: null })).subscribe(page => this.setOptions(page.results));
  }

  private setOptions(seasons: SeasonListItemDto[]): void {
    this._leagues.set(distinct(seasons.map(s => ({ id: s.leagueID, name: s.leagueName }))));
    this._games.set(distinct(seasons.filter(s => s.gameVersionID != null).map(s => ({ id: s.gameVersionID!, name: s.gameVersionName ?? '' }))));
    this._years.set([...new Set(seasons.map(s => s.year))].sort((a, b) => b - a));
  }
}

/** The filter as the generated GetSeasonList operation takes it. */
function listParams(filter: SeasonListFilter) {
  return { leagueID: filter.leagueID ?? undefined, gameVersionID: filter.gameVersionID ?? undefined, format: filter.format ?? undefined, year: filter.year ?? undefined, pageSize: SEASONS_PAGE_SIZE };
}

function distinct(options: Option[]): Option[] {
  const byID = new Map(options.map(o => [o.id, o]));
  return [...byID.values()].sort((a, b) => a.name.localeCompare(b.name));
}
