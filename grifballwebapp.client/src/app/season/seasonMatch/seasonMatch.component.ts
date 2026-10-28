import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, effect, input, signal, Signal, WritableSignal, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { parseSeasonMatchPage, SeasonMatchPageDto } from './seasonMatchPageDto';
import { MatGridListModule } from '@angular/material/grid-list';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { RegexMatchValidValidatorDirective } from '../../validation/directives/regexMatchValidValidatorDirective.directive';
import { PossibleMatchDto, PossiblePlayerDto } from './possibleMatchDto';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatTableModule } from '@angular/material/table';
import { AccountService } from '../../account.service';
import { map } from 'rxjs';

import { ToDatePipe } from '../../shared/toDate.pipe';

export type ForfeitAction = 'HomeForfeit' | 'AwayForfeit' | 'DoubleForfeit';

/** What confirming each forfeit does. */
export const forfeitPrompts: Record<ForfeitAction, string> = {
  HomeForfeit: 'The home team forfeits and the away team wins. This cannot be undone.',
  AwayForfeit: 'The away team forfeits and the home team wins. This cannot be undone.',
  DoubleForfeit: 'Both teams forfeit and take a loss. In the playoffs nobody advances: the matches waiting on this one get a BYE instead. This cannot be undone.',
};

@Component({
    selector: 'app-season-match',
    imports: [
        CommonModule,
        MatGridListModule,
        RouterModule,
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
        RegexMatchValidValidatorDirective,
        MtxDatetimepickerModule,
        MatCardModule,
        MatListModule,
        MatTableModule,
        ToDatePipe,
    ],
    templateUrl: './seasonMatch.component.html',
    styleUrl: './seasonMatch.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonMatchComponent {
  private http = inject(HttpClient);
  accountService = inject(AccountService);

  seasonMatchID: Signal<number> = input.required<number>()
  seasonMatch: WritableSignal<SeasonMatchPageDto | null> = signal(null);

  isSubmittingMatch: WritableSignal<boolean> = signal(false);

  /** The forfeit waiting for the commissioner to confirm it. */
  pendingForfeit: WritableSignal<ForfeitAction | null> = signal(null);
  readonly forfeitPrompts = forfeitPrompts;

  possibleMatches: WritableSignal<PossibleMatchDto[]> = signal([]);

  public displayedColumns: string[] = ['gamertag', 'score', 'kills', 'deaths'];

  columns = [
    {
      columnDef: 'gamertag',
      header: 'Gamertag',
      cell: (element: PossiblePlayerDto) => `${element.gamertag}`,
    },
    {
      columnDef: 'score',
      header: 'Score',
      cell: (element: PossiblePlayerDto) => `${element.score}`,
    },
    {
      columnDef: 'kills',
      header: 'Kills',
      cell: (element: PossiblePlayerDto) => `${element.kills}`,
    },
    {
      columnDef: 'deaths',
      header: 'Deaths',
      cell: (element: PossiblePlayerDto) => `${element.deaths}`,
    },
  ];

  matchID: string = "";
  regex: string = String.raw`^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$`;
  regexErrorMessage: string = "Must be a valid GUID";

  constructor() {
    effect(() => {
      this.seasonMatchID();
      this.getPageDto();
      this.getPossibleMatches();
    })
  }

  getPageDto(): void {
    this.http.get<unknown>('/api/SeasonMatch/GetSeasonMatchPage/' + this.seasonMatchID())
      .pipe(map(parseSeasonMatchPage))
      .subscribe({
        next: result => this.seasonMatch.set(result),
      });
  }

  getPossibleMatches(): void {
    this.http.get<PossibleMatchDto[]>('/api/SeasonMatch/GetPossibleMatches/' + this.seasonMatchID())
      .subscribe({
        next: result => this.possibleMatches.set(result),
      });
  }

  onSubmit(matchID: string): void {
    this.isSubmittingMatch.set(true);

    this.http.get<string>('/api/SeasonMatch/ReportMatch/' + this.seasonMatchID() + '/' + matchID)
        .subscribe({
          next: result => console.log(result),
          error: result => console.log(result),
        }).add(() => this.finishedReportingMatch());
  }

  /** Asks the commissioner to confirm a forfeit before it is recorded. */
  requestForfeit(action: ForfeitAction): void {
    this.pendingForfeit.set(action);
  }

  cancelForfeit(): void {
    this.pendingForfeit.set(null);
  }

  confirmForfeit(): void {
    const action = this.pendingForfeit();
    if (action === null)
      return;

    this.pendingForfeit.set(null);
    this.isSubmittingMatch.set(true);

    this.http.post<string>('/api/SeasonMatch/' + action + '/' + this.seasonMatchID(), null)
      .subscribe({
        next: result => console.log(result),
        error: result => console.log(result),
      }).add(() => this.finishedReportingMatch());
  }

  finishedReportingMatch(): void {
    this.isSubmittingMatch.set(false);
    this.getPageDto();
  }
}
