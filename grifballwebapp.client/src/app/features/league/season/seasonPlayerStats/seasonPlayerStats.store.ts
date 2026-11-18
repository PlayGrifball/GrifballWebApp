import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { SeasonLiveDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';
import { liveData } from '@core/live/liveReload';
import { seasonTopics } from '@core/live/liveTopics';

/** A season's player stats, or one team's. One per table (the component provides it). */
@Injectable()
export class SeasonPlayerStatsStore {
  private readonly api = inject(SeasonService);

  /** The season: the component's input. */
  readonly seasonID = new StoreInput<number>();
  /** Only this team's players (null: the whole season): the component's input. */
  readonly teamID = new StoreInput<number | null>();

  // A new season or team starts over (no rows) rather than showing the last one's while it loads.
  private readonly resource = resourceView(rxResource({
    params: () => {
      const seasonID = this.seasonID.value();
      return seasonID === undefined ? undefined : { seasonID, teamID: this.teamID.value() ?? undefined };
    },
    stream: ({ params }) => this.api.getSeasonPlayerStats(params),
  }), []);

  /** The rows as the server sent them (none while loading, or if they could not be loaded). */
  readonly stats = this.resource.value;
  readonly loaded = computed(() => this.resource.status() !== 'loading');

  constructor() {
    // A game or a stats sheet of the season: the season's stats as they are now come with it; a team's (its page) are read
    liveData<SeasonLiveDto>(() => seasonTopics(this.seasonID.value()), season => {
      if (season.seasonID !== this.seasonID.value())
        return true;
      if (this.teamID.value() != null)
        return false;
      this.resource.set(season.playerStats);
      return true;
    }, () => this.resource.reload());
  }
}
