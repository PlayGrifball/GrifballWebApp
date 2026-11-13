import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, input, signal, WritableSignal, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { DateTime } from 'luxon';
import { map, Observable } from 'rxjs';
import { TeamService } from '@api/services/team.service';
import { fromApiDate } from '@shared/apiDates';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';

import { ToDatePipe } from '@shared/toDate.pipe';
import { SeasonTabsComponent } from '../seasonTabs/seasonTabs.component';
import { BackfilledBannerComponent, DataNoticeComponent } from '@shared/dataNotice/dataNotice.component';
import { SeasonPlayerStatsComponent } from '../seasonPlayerStats/seasonPlayerStats.component';
import { VodListComponent } from '@shared/vodList/vodList.component';
import type { TeamDto } from '@api/models';
import { describePage, PageDescription } from '../../shared/pageMeta';

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
        CommonModule,
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
    ],
    templateUrl: './team.component.html',
    styleUrl: './team.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeamComponent {
  private api = inject(TeamService);


  teamID = input.required<number>();
  seasonID = input.required<number>();

  team: WritableSignal<TeamDto | null> = signal(null);
  matches: MatTableDataSource<MatchDto> = new MatTableDataSource<MatchDto>();
  /** The matches have loaded: a backfilled or ended season without any says so. */
  readonly matchesLoaded = signal(false);
  readonly matchCount = signal(0);
  /** No matches, and none to come: the team's season was backfilled or is over. */
  readonly noMatches = computed(() => {
    const team = this.team();
    return !!team && (team.season.isHistorical || team.season.hasEnded) && this.matchesLoaded() && this.matchCount() === 0;
  });

  displayedColumns: string[] = ['otherTeam', 'score', 'result', 'scheduledTime', 'matchPage'];

  constructor() {
    describePage(() => {
      const team = this.team();
      return team ? describeTeam(team) : null;
    });
    // Another team: nothing of the previous one stays (its name would title this page), and its requests are cancelled
    // so a late answer can't land on this one
    effect(onCleanup => {
      this.teamID();
      this.seasonID();
      this.team.set(null);
      this.matchesLoaded.set(false);
      const team = this.getTeam().subscribe({
        next: (v) => this.team.set(v),
      });

      const matches = this.getMatches().subscribe({
        next: (v) => {
          this.matches.data = v;
          this.matchCount.set(v.length);
          this.matchesLoaded.set(true);
        },
      });
      onCleanup(() => {
        team.unsubscribe();
        matches.unsubscribe();
      });
    })
  }

  getTeam(): Observable<TeamDto | null> {
    return this.api.team({ teamID: this.teamID() }).pipe(nullWhenNotFound());
  }

  getMatches(): Observable<MatchDto[]> {
    return this.api.matches({ teamID: this.teamID() })
      .pipe(map(matches => matches.map(parseTeamMatch)));
  }
}

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
export function parseTeamMatch(raw: any): MatchDto {
  return { ...raw, scheduledTime: fromApiDate(raw.scheduledTime) };
}
