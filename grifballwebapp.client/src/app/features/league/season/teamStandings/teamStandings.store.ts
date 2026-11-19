import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { SeasonLiveDto } from '@api/models';
import { TeamStandingsService } from '@api/services/team-standings.service';
import { StoreInput } from '@shared/storeInput';
import { tap } from 'rxjs';
import { resourceView } from '@shared/resourceView';
import { AppLog } from '@core/telemetry/appLog';
import { liveData } from '@core/live/liveReload';
import { seasonTopics } from '@core/live/liveTopics';

/** A season's standings, loaded again for each season (none for 0). One per table (the component provides it). */
@Injectable()
export class TeamStandingsStore {
  private readonly log = inject(AppLog);
  private readonly api = inject(TeamStandingsService);

  /** The season page's season (0: none yet): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** A new season drops the request still on its way. */
  private readonly standings = resourceView(rxResource({
    params: () => this.seasonID.value() || undefined,
    stream: ({ params: seasonID }) => this.api.getTeamStandings({ seasonID }).pipe(tap({ error: e => this.log.error('Failed to load the standings', e) })),
  }), []);
  readonly teamStandings = this.standings.value;
  /** The standings have loaded (or failed to, or there is no season). */
  readonly loaded = computed(() => this.standings.status() !== 'loading');

  constructor() {
    // A result, a team, a forfeit: the standings as they are now come with it
    liveData<SeasonLiveDto>(() => seasonTopics(this.seasonID.value()), season => {
      if (season.seasonID === this.seasonID.value())
        this.standings.set(season.standings);
      return true;
    }, () => this.standings.reload());
  }
}
