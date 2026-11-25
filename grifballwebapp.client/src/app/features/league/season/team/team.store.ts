import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { TeamDto, TeamLiveDto, TeamPageMatchDto } from '@api/models';
import { TeamService } from '@api/services/team.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { fromApiDate } from '@shared/apiDates';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';
import { DateTime } from 'luxon';
import { map } from 'rxjs';
import { liveData, liveReload } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';

export interface MatchDto
{
  seasonMatchID: number,
  scheduledTime: DateTime | null, // DateTime? on the server: an unscheduled match has none
  bestOf: number,
  score: number | null,
  result: 'Won' | 'Loss' | 'Forfeit' | 'Bye' | null,
  otherTeamID: number | null,
  otherTeamName: string | null,
  otherScore: number | null,
  otherResult: 'Won' | 'Loss' | 'Forfeit' | 'Bye' | null,
}

/** A team match as the API sends it (scheduledTime as an ISO string or null), with a real DateTime. */
export function parseTeamMatch(raw: TeamPageMatchDto): MatchDto {
  return { ...raw, scheduledTime: fromApiDate(raw.scheduledTime) };
}

/** A season's team and its matches. One per page (the component provides it). */
@Injectable()
export class TeamStore {
  private readonly api = inject(TeamService);

  /** The team: the component's input. */
  readonly teamID = new StoreInput<number>();
  /** The team's season: the component's input (another one loads the team again). */
  readonly seasonID = new StoreInput<number>();

  /** The team and its season, once both are known: another drops the requests still on their way. */
  private readonly shown = computed(() => {
    const teamID = this.teamID.value();
    const seasonID = this.seasonID.value();
    return teamID === undefined ? undefined : { teamID, seasonID };
  });

  private readonly loadedTeam = resourceView(rxResource({
    params: () => this.shown(),
    stream: ({ params }) => this.api.team({ teamID: params.teamID }).pipe(nullWhenNotFound()),
  }), null);
  /** The team; null while it loads, when there is no such team, or when it failed. */
  readonly team = this.loadedTeam.value;
  /** The team is on its way: the roster is a skeleton. */
  readonly teamFirstLoad = this.loadedTeam.firstLoad;

  private readonly loadedMatches = resourceView(rxResource({
    params: () => this.shown(),
    stream: ({ params }) => this.api.matches({ teamID: params.teamID }).pipe(map(m => m.map(parseTeamMatch))),
  }), []);
  /** The team's matches; none while they load, or when they failed (see matchesFailed). */
  readonly matches = this.loadedMatches.value;
  /** The matches are on their way: the table is a skeleton. */
  readonly matchesFirstLoad = this.loadedMatches.firstLoad;
  /** The matches could not be read: the page says so, never that none were recorded. */
  readonly matchesFailed = this.loadedMatches.failed;
  /** The matches are in (read, or pushed with a live change): a backfilled or ended season without any says so. */
  readonly matchesLoaded = this.loadedMatches.hasValue;

  constructor() {
    // A result, a roster change, a rename: the team and its matches as they are now come with it, in place
    liveData<TeamLiveDto>(() => {
      const teamID = this.teamID.value();
      return teamID ? [LiveTopics.team(teamID)] : [];
    }, team => {
      if (team.teamID === this.teamID.value()) {
        this.loadedTeam.set(team.team ?? null);
        this.loadedMatches.set(team.matches.map(parseTeamMatch));
      }
      return true;
    }, () => this.reload());
    // What team:N doesn't hear: its season edited, scheduled or announced, its champions (the title badges), an opponent
    // renamed (the matches name it). The season's teams page topic does, so a change there reads the team again (its data
    // is that page's, not this one's). The alternative: the server tells each of the season's teams' topics of those
    // events, looking the teams up as it delivers them (as it looks up a changed Xbox account's profiles).
    liveReload(() => {
      const seasonID = this.seasonID.value();
      return seasonID ? [LiveTopics.teams(seasonID)] : [];
    }, () => this.reload());
  }

  /** Reads the team and its matches again, in place: what is shown stays until the answers come. */
  private reload(): void {
    this.loadedTeam.reload();
    this.loadedMatches.reload();
  }
}
