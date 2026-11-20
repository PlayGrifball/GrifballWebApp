import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { SeasonLiveDto, TeamLiveDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';
import { liveData } from '@core/live/liveReload';
import { LiveTopics, seasonTopics } from '@core/live/liveTopics';

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
    // A game or a stats sheet: the season's stats as they are now come with the season's page, a team's with its page
    liveData<SeasonLiveDto | TeamLiveDto>(() => {
      const teamID = this.teamID.value();
      return teamID == null ? seasonTopics(this.seasonID.value()) : [LiveTopics.team(teamID)];
    }, page => {
      const current = 'teamID' in page ? page.teamID === this.teamID.value() : page.seasonID === this.seasonID.value() && this.teamID.value() == null;
      if (current)
        this.resource.set(page.playerStats);
      return true;
    }, () => this.resource.reload());
  }
}
