import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, effect, input, signal, Signal, ViewChild, WritableSignal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SeasonMatchPageDto } from './seasonMatchPageDto';
import { MatGridListModule } from '@angular/material/grid-list';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule, NgForm } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { RegexMatchValidValidatorDirective } from '../../validation/directives/regexMatchValidValidatorDirective.directive';
import { PossibleMatchDto, PossiblePlayerDto } from './possibleMatchDto';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatTableModule } from '@angular/material/table';
import { AccountService } from '../../account.service';

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
        MatTableModule
    ],
    templateUrl: './seasonMatch.component.html',
    styleUrl: './seasonMatch.component.scss'
})
export class SeasonMatchComponent {
  seasonMatchID: Signal<number> = input.required<number>()
  seasonMatch: WritableSignal<SeasonMatchPageDto | null> = signal(null);

  isSubmittingMatch: WritableSignal<boolean> = signal(false);

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

  @ViewChild('reportMatchForm') reportMatchForm!: NgForm;

  matchID: string = "";
  regex: string = String.raw`^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$`;
  regexErrorMessage: string = "Must be a valid GUID";

  constructor(private http: HttpClient, public accountService: AccountService) {
    effect(() => {
      this.seasonMatchID();
      this.getPageDto();
      this.getPossibleMatches();
    })
  }

  getPageDto(): void {
    this.http.get<SeasonMatchPageDto>('/api/SeasonMatch/GetSeasonMatchPage/' + this.seasonMatchID())
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

  homeForfeit(): void {
    this.isSubmittingMatch.set(true);

    this.http.get<string>('/api/SeasonMatch/HomeForfeit/' + this.seasonMatchID())
      .subscribe({
        next: result => console.log(result),
        error: result => console.log(result),
      }).add(() => this.finishedReportingMatch());
  }

  awayForfeit(): void {
    this.isSubmittingMatch.set(true);

    this.http.get<string>('/api/SeasonMatch/AwayForfeit/' + this.seasonMatchID())
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
