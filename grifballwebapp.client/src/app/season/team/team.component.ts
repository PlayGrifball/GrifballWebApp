import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, input, signal, WritableSignal, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { DateTime } from 'luxon';
import { map, Observable } from 'rxjs';
import { fromApiDate } from '../../shared/apiDates';

import { ToDatePipe } from '../../shared/toDate.pipe';
import { SeasonTabsComponent } from '../seasonTabs/seasonTabs.component';
import { BackfilledBannerComponent, DataNoticeComponent } from '../../shared/dataNotice/dataNotice.component';
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
    ],
    templateUrl: './team.component.html',
    styleUrl: './team.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeamComponent {
  private http = inject(HttpClient);


  teamID = input.required<number>();
  seasonID = input.required<number>();

  team: WritableSignal<TeamDto | null> = signal(null);
  matches: MatTableDataSource<MatchDto> = new MatTableDataSource<MatchDto>();
  /** The matches have loaded: a backfilled season without any says so. */
  readonly matchesLoaded = signal(false);
  readonly matchCount = signal(0);

  displayedColumns: string[] = ['otherTeam', 'score', 'result', 'scheduledTime', 'matchPage'];

  constructor() {
    effect(() => {
      this.teamID();
      this.seasonID();
      this.getTeam().subscribe({
        next: (v) => this.team.set(v),
      });
  
      this.getMatches().subscribe({
        next: (v) => {
          this.matches.data = v;
          this.matchCount.set(v.length);
          this.matchesLoaded.set(true);
        },
      });
    })
  }

  getTeam(): Observable<TeamDto | null> {
    return this.http.get<TeamDto | null>("/api/Team/Team/" + this.teamID());
  }

  getMatches(): Observable<MatchDto[]> {
    return this.http.get<unknown[]>("/api/Team/Matches/" + this.teamID())
      .pipe(map(matches => matches.map(parseTeamMatch)));
  } 
}

export interface TeamDto
{
  teamName: string,
  seasonID?: number,
  /** The season was backfilled from the historical archive; historicalSourceUrl is its spreadsheet. */
  isHistorical?: boolean,
  historicalSourceUrl?: string | null,
  /** wins and losses are the source spreadsheet's: the season's matches are not in the app. */
  recordFromSource?: boolean,
  /** The tiers whose title the team won. */
  titles?: string[],
  stats?: TeamPlayerStatDto[],
  wins: number,
  losses: number,
  tbd: number,
  players: PlayerDto[],
}

export interface PlayerDto
{
  teamPlayerID: number,
  userID: number,
  /** null for a player without an Xbox account linked */
  gamertag: string | null,
  draftCaptainOrder: number | null,
  draftRound: number | null,
  /** Its drafted captain, or the one its title names. */
  isCaptain?: boolean,
}

/** A player's totals for the team in one phase of the season. */
export interface TeamPlayerStatDto
{
  userID: number,
  gamertag: string,
  isPlayoffs: boolean,
  stintIndex: number,
  tradeNote?: string | null,
  roleNote?: string | null,
  gamesPlayed: number,
  wins: number,
  losses: number,
  goals: number,
  kills: number,
  deaths: number,
  kdRatio?: number | null,
  carrierKills: number,
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
