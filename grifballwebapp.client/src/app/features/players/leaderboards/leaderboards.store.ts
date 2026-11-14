import { computed, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { LeaderboardFilter, LeaderboardsDto, PlayerAllTimeRankDto } from '@api/models';
import { LeaderboardsService } from '@api/services/leaderboards.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { leaderboardFormats } from '@shared/seasonFormats';
import { catchError, EMPTY, map, of, Subject, switchMap, tap } from 'rxjs';

export const ALL_FORMATS: LeaderboardFilter[] = leaderboardFormats.map(f => f.value);

/** A player to show the standing of: by user ID when picked from the suggestions, else by the text searched. */
export interface Lookup { query: string, userId: number | null }

/**
 * The all-time leaderboards for the formats chosen, and a player's standing on them. Each load and each lookup
 * cancels the one in flight, so what is shown is always what was asked for last. One per page (the component provides it).
 */
@Injectable()
export class LeaderboardsStore {
  private readonly api = inject(LeaderboardsService);

  private readonly _boards = signal<LeaderboardsDto | null>(null);
  readonly boards = this._boards.asReadonly();
  private readonly _loaded = signal(false);
  readonly loaded = this._loaded.asReadonly();
  /** The formats every board counts, titles and stats alike: any of them, all of them to start with. */
  private readonly _formats = signal<LeaderboardFilter[]>(ALL_FORMATS);
  readonly formats = this._formats.asReadonly();
  /** The formats asked of the server: none named when every one is (its default). */
  private readonly formatsAsked = computed(() => {
    const chosen = this.formats();
    return chosen.length === ALL_FORMATS.length ? undefined : chosen;
  });

  private readonly _spotlight = signal<PlayerAllTimeRankDto | null>(null);
  readonly spotlight = this._spotlight.asReadonly();
  private readonly _spotlightLoading = signal(false);
  readonly spotlightLoading = this._spotlightLoading.asReadonly();
  private readonly _spotlightError = signal<string | null>(null);
  readonly spotlightError = this._spotlightError.asReadonly();

  /** The player last asked for (null: none), shown or still loading: a new format looks them up again. */
  private requested: Lookup | null = null;
  /** Each lookup (or null: none) cancels the one in flight, so an older player's standing never replaces a newer one. */
  private readonly lookups = new Subject<Lookup | null>();
  /** Each load cancels the one in flight, so the boards shown are always the formats chosen last. */
  private readonly loads = new Subject<LeaderboardFilter[] | undefined>();

  constructor() {
    this.lookups.pipe(
      tap(lookup => {
        this._spotlightError.set(null);
        this._spotlightLoading.set(lookup != null);
        if (!lookup) this._spotlight.set(null);
      }),
      switchMap(lookup => !lookup ? EMPTY : this.api.getPlayerRank({ query: lookup.query || undefined, userID: lookup.userId ?? undefined, formats: this.formatsAsked() })
        .pipe(
        nullWhenNotFound(), // 404: no player matches
        map(rank => ({ lookup, rank, failed: false })),
        catchError(() => of({ lookup, rank: null, failed: true })),
      )),
      takeUntilDestroyed(),
    ).subscribe(({ lookup, rank, failed }) => {
      this._spotlightLoading.set(false);
      this._spotlight.set(rank);
      if (!rank)
        this._spotlightError.set(failed ? `Failed to load ranking for "${lookup.query}".` : `No player found matching "${lookup.query}".`);
    });

    this.loads.pipe(
      switchMap(formats => this.api.getLeaderboards({ formats }).pipe(catchError(() => of(null)))),
      takeUntilDestroyed(),
    ).subscribe(boards => {
      if (boards)
        this._boards.set(boards);
      this._loaded.set(true);
    });
  }

  /** Loads the boards for the formats chosen. */
  load(): void {
    this.loads.next(this.formatsAsked());
  }

  /** Counts the formats chosen, in the select's order; none chosen counts them all again. */
  setFormats(chosen: LeaderboardFilter[]): void {
    this._formats.set(chosen.length === 0 ? ALL_FORMATS : ALL_FORMATS.filter(f => chosen.includes(f)));
    this.load();
    // The standing counts the formats too: the player last asked for, even while their standing is loading
    if (this.requested)
      this.lookUp(this.requested);
  }

  /** Shows a player's standing (null: closes it). */
  lookUp(lookup: Lookup | null): void {
    this.requested = lookup;
    this.lookups.next(lookup);
  }
}
