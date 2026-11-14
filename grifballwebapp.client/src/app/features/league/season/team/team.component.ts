import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { ToDatePipe } from '@shared/toDate.pipe';
import { SeasonTabsComponent } from '../seasonTabs/seasonTabs.component';
import { BackfilledBannerComponent, DataNoticeComponent } from '@shared/dataNotice/dataNotice.component';
import { SeasonPlayerStatsComponent } from '../seasonPlayerStats/seasonPlayerStats.component';
import { VodListComponent } from '@shared/vodList/vodList.component';
import type { TeamDto } from '@api/models';
import { describePage, PageDescription } from '@shared/pageMeta';
import { TeamStore } from './team.store';

/** The team as search results and link previews show it: its name and season, record, titles and roster. */
export function describeTeam(team: TeamDto): PageDescription {
  // The tiers it won, named as titles ("Gold champions", not a bare "Gold")
  const record = [`${team.wins}–${team.losses}`, ...team.titles.map(tier => `${tier} champions`)].join(', ');
  const season = `${team.season.leagueName} ${team.season.seasonName}`;
  const parts = [`${team.teamName} in ${season}: ${record}.`];
  if (team.players.length)
    parts.push(`Roster: ${team.players.map(p => p.gamertag).join(', ')}.`);
  parts.push('Matches and player stats.');
  return {
    title: `${team.teamName} · ${team.season.seasonName}`,
    description: parts.join(' '),
  };
}

@Component({
    selector: 'app-team',
    imports: [
        MatButtonModule,
        SeasonTabsComponent,
        MatTableModule,
        RouterModule,
        MatIcon,
        ToDatePipe,
        BackfilledBannerComponent,
        DataNoticeComponent,
        SeasonPlayerStatsComponent,
        VodListComponent,
        DatePipe,
    ],
    templateUrl: './team.component.html',
    styleUrl: './team.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [TeamStore],
})
export class TeamComponent {
  private readonly store = inject(TeamStore);

  teamID = input.required<number>();
  seasonID = input.required<number>();

  readonly team = this.store.team;
  readonly matches = this.store.matches;
  /** The matches have loaded: a backfilled or ended season without any says so. */
  readonly matchesLoaded = this.store.matchesLoaded;
  readonly matchCount = computed(() => this.matches().length);
  /** No matches, and none to come: the team's season was backfilled or is over. */
  readonly noMatches = computed(() => {
    const team = this.team();
    return !!team && (team.season.isHistorical || team.season.hasEnded) && this.matchesLoaded() && this.matchCount() === 0;
  });

  displayedColumns: string[] = ['otherTeam', 'score', 'result', 'scheduledTime', 'matchPage'];

  constructor() {
    this.store.teamID.use(this.teamID);
    this.store.seasonID.use(this.seasonID);
    describePage(() => {
      const team = this.team();
      return team ? describeTeam(team) : null;
    });
  }
}
