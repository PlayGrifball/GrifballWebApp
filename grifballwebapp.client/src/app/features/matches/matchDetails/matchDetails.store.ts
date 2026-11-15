import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatchDetailsService } from '@api/services/match-details.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { resourceView } from '@shared/resourceView';
import { StoreInput } from '@shared/storeInput';
import { map } from 'rxjs';
import { parseMatchDetails } from './matchDetailsDto';

/** What the page shows: loading, the match, no such match pulled, or the request failed. */
export type MatchDetailsState = 'loading' | 'loaded' | 'notFound' | 'error';

/** A Halo Infinite match's details, by its Waypoint match ID. One per page (the component provides it). */
@Injectable()
export class MatchDetailsStore {
  private readonly api = inject(MatchDetailsService);

  /** The Waypoint match ID: the component's input. */
  readonly matchID = new StoreInput<string>();

  /** The match, loaded again for each match ID: a new one cancels the request still on its way (and so does leaving the page). */
  private readonly match = resourceView(rxResource({
    params: () => {
      const matchID = this.matchID.value();
      return matchID === undefined ? undefined : { matchID };
    },
    // No such match: a 404 (a malformed ID misses the route's guid constraint, so it is one too)
    stream: ({ params }) => this.api.getMatchDetails(params).pipe(nullWhenNotFound(), map(parseMatchDetails)),
  }), null);

  readonly details = this.match.value;
  /**
   * Loading (also before the match ID is handed over: nothing is asked for yet); loaded; no such match (none, or a
   * 404); or the request failed.
   */
  readonly state = computed<MatchDetailsState>(() => {
    if (this.match.isLoading() || this.match.status() === 'idle')
      return 'loading';
    if (this.match.failed())
      return 'error';
    return this.details() ? 'loaded' : 'notFound';
  });
}
