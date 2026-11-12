import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { SeasonInfoDto } from '@api/models';
import type { PlayerDto } from '../season/team/team.component';
import { nullWhenNotFound } from '../api/nullWhenNotFound';

/**
 * A franchise's history: every season's team of one name in one league (a team is a row of one season), found from
 * any one of them, newest first, with its record, roster, captain and titles, and the franchise's totals.
 */
@Component({
  selector: 'app-team-history',
  imports: [RouterModule],
  templateUrl: './teamHistory.component.html',
  styleUrl: './teamHistory.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamHistoryComponent {
  private readonly http = inject(HttpClient);

  /** One of the franchise's teams (any season's), as the route gives it: the server finds its name and league. */
  readonly teamID = input.required<number>();

  /** A new team cancels the request before it and clears what it showed. */
  readonly historyResource = rxResource({
    params: () => this.teamID(),
    stream: ({ params: teamID }) => this.http.get<TeamHistoryDto>(`/api/teams/${teamID}/history`).pipe(nullWhenNotFound()),
  });
  readonly history = computed(() => this.historyResource.hasValue() ? this.historyResource.value() ?? null : null);
  readonly loaded = computed(() => !this.historyResource.isLoading());
  readonly failed = computed(() => this.historyResource.status() === 'error');

  /** The season's captain(s): drafted, or named by its title. */
  captains(season: TeamHistorySeasonDto): PlayerDto[] {
    return season.players.filter(p => p.isCaptain);
  }
}

/** A franchise: every season's team of one name in one league, and its totals (TeamHistoryDto). */
export interface TeamHistoryDto {
  /** The name of its latest season's team. */
  teamName: string;
  /** The league it played in. */
  leagueName: string;
  appearances: number;
  wins: number;
  losses: number;
  /** The titles it won, across seasons and tiers. */
  championships: number;
  /** The players who played for it, each once. */
  players: number;
  /** Newest first. */
  seasons: TeamHistorySeasonDto[];
}

/** One season of a franchise: that season's team. */
export interface TeamHistorySeasonDto {
  teamID: number;
  /** The name as that season spelled it. */
  teamName: string;
  season: SeasonInfoDto;
  wins: number;
  losses: number;
  /** wins and losses are the source spreadsheet's: the season's matches are not in the app. */
  recordFromSource?: boolean;
  /** The tiers whose title the team won. */
  titles: string[];
  /** Captain first, then by draft round. */
  players: PlayerDto[];
}
