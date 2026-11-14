import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TeamStandingsService } from '@api/services/team-standings.service';
import { StoreInput } from '@shared/storeInput';
import { tap } from 'rxjs';
import { TeamStandingDto } from './teamStandingDto';

/** A season's standings, loaded again for each season (none for 0). One per table (the component provides it). */
@Injectable()
export class TeamStandingsStore {
  private readonly api = inject(TeamStandingsService);

  /** The season page's season (0: none yet): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** A new season drops the request still on its way. */
  private readonly standings = rxResource({
    params: () => this.seasonID.value() || undefined,
    stream: ({ params: seasonID }) => this.api.getTeamStandings({ seasonID }).pipe(tap({ error: e => console.log(e) })),
  });
  readonly teamStandings = computed<TeamStandingDto[]>(() => this.standings.hasValue() ? this.standings.value() : []);
  /** The standings have loaded (or failed to, or there is no season). */
  readonly loaded = computed(() => !this.standings.isLoading());
}
