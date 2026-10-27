import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, input, signal, WritableSignal, inject } from '@angular/core';
import { MatCard, MatCardContent, MatCardHeader } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { DateTime } from 'luxon';
import { map, Observable } from 'rxjs';
import { fromApiDate } from '../../shared/apiDates';

import { ToDatePipe } from '../../shared/toDate.pipe';
@Component({
    selector: 'app-team',
    imports: [
        CommonModule,
        MatCard,
        MatCardHeader,
        MatCardContent,
        MatTableModule,
        RouterModule,
        MatIcon,
        ToDatePipe,
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

  displayedColumns: string[] = ['otherTeam', 'score', 'result', 'scheduledTime', 'matchPage'];

  constructor() {
    effect(() => {
      this.teamID();
      this.seasonID();
      this.getTeam().subscribe({
        next: (v) => this.team.set(v),
      });
  
      this.getMatches().subscribe({
        next: (v) => this.matches.data = v,
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
  wins: number,
  losses: number,
  tbd: number,
  players: PlayerDto[],
}

export interface PlayerDto
{
  teamPlayerID: number,
  userID: number,
  gamertag: string,
  draftCaptainOrder: number | null,
  draftRound: number | null,
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
