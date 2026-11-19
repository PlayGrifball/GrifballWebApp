import { effect, inject, Injectable, signal } from '@angular/core';
import type { TeamDto, TeamLiveDto, TeamPageMatchDto } from '@api/models';
import { TeamService } from '@api/services/team.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { fromApiDate } from '@shared/apiDates';
import { StoreInput } from '@shared/storeInput';
import { DateTime } from 'luxon';
import { map } from 'rxjs';
import { liveData } from '@core/live/liveReload';
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

  private readonly _team = signal<TeamDto | null>(null);
  readonly team = this._team.asReadonly();
  private readonly _matches = signal<MatchDto[]>([]);
  readonly matches = this._matches.asReadonly();
  /** The matches have loaded: a backfilled or ended season without any says so. */
  private readonly _matchesLoaded = signal(false);
  readonly matchesLoaded = this._matchesLoaded.asReadonly();
  /** Bumped by a live change: the team loaded again, in place. */
  private readonly changes = signal(0);

  constructor() {
    // Another team: nothing of the previous one stays (its name would title the page), and its requests are cancelled
    // so a late answer can't land on this one. A live change loads the same team again, keeping it shown meanwhile.
    let shown: string | undefined;
    effect(onCleanup => {
      const teamID = this.teamID.value();
      const seasonID = this.seasonID.value();
      this.changes();
      if (teamID === undefined)
        return;
      if (shown !== `${teamID}:${seasonID}`) {
        shown = `${teamID}:${seasonID}`;
        this._team.set(null);
        this._matchesLoaded.set(false);
      }
      const team = this.api.team({ teamID }).pipe(nullWhenNotFound()).subscribe({
        next: v => this._team.set(v),
      });
      const matches = this.api.matches({ teamID }).pipe(map(m => m.map(parseTeamMatch))).subscribe({
        next: v => {
          this._matches.set(v);
          this._matchesLoaded.set(true);
        },
      });
      onCleanup(() => {
        team.unsubscribe();
        matches.unsubscribe();
      });
    });
    // A result, a roster change, a rename: the team and its matches as they are now come with it, in place
    liveData<TeamLiveDto>(() => {
      const teamID = this.teamID.value();
      return teamID ? [LiveTopics.team(teamID)] : [];
    }, team => {
      if (team.teamID === this.teamID.value()) {
        this._team.set(team.team ?? null);
        this._matches.set(team.matches.map(parseTeamMatch));
        this._matchesLoaded.set(true);
      }
      return true;
    }, () => this.changes.update(n => n + 1));
  }
}
