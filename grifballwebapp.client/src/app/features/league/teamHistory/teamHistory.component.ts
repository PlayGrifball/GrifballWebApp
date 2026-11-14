import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterModule } from '@angular/router';
import type { TeamHistoryDto, TeamHistorySeasonDto, TeamPagePlayerDto } from '@api/models';
import { describePage, PageDescription, plural } from '@shared/pageMeta';
import { TeamHistoryStore } from './teamHistory.store';

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
  providers: [TeamHistoryStore],
})
export class TeamHistoryComponent {
  private readonly store = inject(TeamHistoryStore);

  /** One of the franchise's teams (any season's), as the route gives it: the server finds its name and league. */
  readonly teamID = input.required<number>();

  readonly history = this.store.history;
  readonly loaded = this.store.loaded;
  readonly failed = this.store.failed;

  constructor() {
    this.store.teamID.use(this.teamID);
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
