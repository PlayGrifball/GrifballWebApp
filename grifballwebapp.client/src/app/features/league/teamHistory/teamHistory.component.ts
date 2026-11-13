import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { TeamService } from '@api/services/team.service';
import type { TeamHistoryDto, TeamHistorySeasonDto, TeamPagePlayerDto } from '@api/models';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { describePage, PageDescription, plural } from '@shared/pageMeta';

/** The franchise as search results and link previews show it: its name, league, seasons, record and titles. */
export function describeTeamHistory(history: TeamHistoryDto): PageDescription {
  const facts = [
    plural(history.appearances, 'season'),
    `${history.wins}–${history.losses}`,
    history.championships ? plural(history.championships, 'championship') : null,
  ].filter(f => f);
  return {
    title: `${history.teamName} · Team History`,
    description: `${history.teamName} in ${history.leagueName}: ${facts.join(', ')}. Every season's record, roster and captain.`,
  };
}

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
  private readonly api = inject(TeamService);

  /** One of the franchise's teams (any season's), as the route gives it: the server finds its name and league. */
  readonly teamID = input.required<number>();

  /** A new team cancels the request before it and clears what it showed. */
  readonly historyResource = rxResource({
    params: () => this.teamID(),
    stream: ({ params: teamID }) => this.api.history({ teamID }).pipe(nullWhenNotFound()),
  });
  readonly history = computed(() => this.historyResource.hasValue() ? this.historyResource.value() ?? null : null);
  readonly loaded = computed(() => !this.historyResource.isLoading());
  readonly failed = computed(() => this.historyResource.status() === 'error');

  constructor() {
    describePage(() => {
      const history = this.history();
      return history ? describeTeamHistory(history) : null;
    });
  }

  /** The season's captain(s): drafted, or named by its title. */
  captains(season: TeamHistorySeasonDto): TeamPagePlayerDto[] {
    return season.players.filter(p => p.isCaptain);
  }
}
