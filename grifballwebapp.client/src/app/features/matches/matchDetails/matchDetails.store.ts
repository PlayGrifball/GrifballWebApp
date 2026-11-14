import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatchDetailsService } from '@api/services/match-details.service';
import { StoreInput } from '@shared/storeInput';
import { map } from 'rxjs';
import { MatchDetailsDto, parseMatchDetails } from './matchDetailsDto';

/** What the page shows: loading, the match, no such match pulled, or the request failed. */
export type MatchDetailsState = 'loading' | 'loaded' | 'notFound' | 'error';

/** A Halo Infinite match's details, by its Waypoint match ID. One per page (the component provides it). */
@Injectable()
export class MatchDetailsStore {
  private readonly api = inject(MatchDetailsService);

  /** The Waypoint match ID: the component's input. */
  readonly matchID = new StoreInput<string>();

  /** The match, loaded again for each match ID: a new one cancels the request still on its way (and so does leaving the page). */
  private readonly match = rxResource({
    params: () => {
      const matchID = this.matchID.value();
      return matchID === undefined ? undefined : { matchID };
    },
    stream: ({ params }) => this.api.getMatchDetails(params).pipe(map(parseMatchDetails)),
  });

  readonly details = computed<MatchDetailsDto | null>(() => this.match.hasValue() ? this.match.value() : null);
  /** Loading; loaded; no such match (none, a 404, or an ID that isn't one: 400); or the request failed. */
  readonly state = computed<MatchDetailsState>(() => {
    if (this.match.isLoading())
      return 'loading';
    const error = this.match.error();
    if (error)
      return error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400) ? 'notFound' : 'error';
    return this.details() ? 'loaded' : 'notFound';
  });
}
