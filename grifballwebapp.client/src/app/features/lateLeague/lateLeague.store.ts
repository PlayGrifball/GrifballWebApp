import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { LateLeagueService } from '@api/services/late-league.service';
import { StoreInput } from '@shared/storeInput';
import { catchError, forkJoin, of } from 'rxjs';

/**
 * Late League's players: a season's (with a season), or the queue's as it is played now and the archive's seasons
 * (without one). One per page (the component provides it).
 */
@Injectable()
export class LateLeagueStore {
  private readonly api = inject(LateLeagueService);

  /** The route's season (null: Late League as a whole): the component's. */
  readonly seasonID = new StoreInput<number | null>();

  /** A season's players (with a season); the request still on its way is cancelled when it changes. */
  private readonly season = rxResource({
    params: () => this.seasonID.value() ?? undefined,
    stream: ({ params: matchmakingSeasonID }) => this.api.getLateLeagueSeason({ matchmakingSeasonID }),
  });
  /** Late League as a whole (without one): the queue's players and the archive's seasons, each on its own so one that
   * fails doesn't take the other with it. Nothing until the page hands its season over. */
  private readonly overview = rxResource({
    params: () => this.seasonID.value() === null ? true : undefined,
    stream: () => forkJoin({
      queue: this.api.getLateLeagueQueue().pipe(catchError(() => of(null))),
      seasons: this.api.getLateLeagueSeasons().pipe(catchError(() => of(null))),
    }),
  });

  readonly queue = computed(() => this.overview.hasValue() ? this.overview.value().queue : null);
  readonly seasons = computed(() => this.overview.hasValue() ? this.overview.value().seasons : null);
  readonly detail = computed(() => this.season.hasValue() ? this.season.value() : null);
  /** What the page shows has loaded, or failed to. */
  readonly loaded = computed(() => !this.season.isLoading() && !this.overview.isLoading());
}
