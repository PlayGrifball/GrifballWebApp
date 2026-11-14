import { DecimalPipe } from '@angular/common';
import { Component, ChangeDetectionStrategy, computed, inject, input, numberAttribute } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { LetterDto } from '@api/models';
import { MatTableModule } from '@angular/material/table';
import { SeasonTabsComponent } from '../seasonTabs/seasonTabs.component';
import { DataNoticeComponent } from '@shared/dataNotice/dataNotice.component';
import { PlayerGradesStore } from './playerGrades.store';

@Component({
    selector: 'app-player-grades',
    imports: [
        SeasonTabsComponent,
        MatTableModule,
        DataNoticeComponent,
        RouterLink,
    ],
    providers: [DecimalPipe, PlayerGradesStore],
    templateUrl: './playerGrades.component.html',
    styleUrl: './playerGrades.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PlayerGradesComponent {
  private readonly store = inject(PlayerGradesStore);
  private pipe = inject(DecimalPipe);

  /** The :seasonID of the route. Angular keeps this page when only it changes, so a new one loads that season's grades. */
  readonly seasonID = input.required({ transform: numberAttribute });

  readonly letters = this.store.letters;
  readonly summary = this.store.summary;
  readonly gradesDone = this.store.gradesDone;
  readonly gradesFailed = this.store.gradesFailed;
  /** A backfilled or ended season without grades says so in place of the table. */
  readonly noMatches = this.store.noMatches;
  /** Nothing is drawn until the page knows whether it shows the notice or the table, so neither flashes up first. */
  readonly showTable = computed(() => this.store.summaryDone() && this.gradesDone() && !this.noMatches());

  public letterDisplayedColumns: string[] = ['gamertag', 'goals', 'kdSpread', 'punches', 'sprees', 'doubleKills', 'tripleKills', 'multiKills', 'xFactor', 'kills', 'gradeAvgMath', 'gradeAvg'];

  letterColumns = [
    {
      columnDef: 'gamertag',
      header: 'Gamertag',
      cell: (element: LetterDto) => `${element.gamertag}`,
      style: (element: LetterDto) => '',
      color: '',
    },
    {
      columnDef: 'goals',
      header: 'Goals',
      cell: (element: LetterDto) => `${element.goals}`,
      style: (element: LetterDto) => this.getColor(element.goals),
      color: 'Black',
    },
    {
      columnDef: 'kdSpread',
      header: 'KD Spread',
      cell: (element: LetterDto) => `${element.kdSpread}`,
      style: (element: LetterDto) => this.getColor(element.kdSpread),
      color: 'Black',
    },
    {
      columnDef: 'punches',
      header: 'Punches',
      cell: (element: LetterDto) => `${element.punches}`,
      style: (element: LetterDto) => this.getColor(element.punches),
      color: 'Black',
    },
    {
      columnDef: 'sprees',
      header: 'Sprees',
      cell: (element: LetterDto) => `${element.sprees}`,
      style: (element: LetterDto) => this.getColor(element.sprees),
      color: 'Black',
    },
    {
      columnDef: 'doubleKills',
      header: 'Double Kills',
      cell: (element: LetterDto) => `${element.doubleKills}`,
      style: (element: LetterDto) => this.getColor(element.doubleKills),
      color: 'Black',
    },
    {
      columnDef: 'tripleKills',
      header: 'Triple Kills',
      cell: (element: LetterDto) => `${element.tripleKills}`,
      style: (element: LetterDto) => this.getColor(element.tripleKills),
      color: 'Black',
    },
    {
      columnDef: 'multiKills',
      header: 'MultiKills',
      cell: (element: LetterDto) => `${element.multiKills}`,
      style: (element: LetterDto) => this.getColor(element.multiKills),
      color: 'Black',
    },
    {
      columnDef: 'xFactor',
      header: 'X Factor',
      cell: (element: LetterDto) => `${element.xFactor}`,
      style: (element: LetterDto) => this.getColor(element.xFactor),
      color: 'Black',
    },
    {
      columnDef: 'kills',
      header: 'Kills',
      cell: (element: LetterDto) => `${element.kills}`,
      style: (element: LetterDto) => this.getColor(element.kills),
      color: 'Black',
    },
    {
      columnDef: 'gradeAvgMath',
      header: 'Grade Avg Math',
      cell: (element: LetterDto) => this.pipe.transform(element.gradeAvgMath, '1.2-2'),
      style: (element: LetterDto) => this.getColor(element.gradeAvg),
      color: 'Black',
    },
    {
      columnDef: 'gradeAvg',
      header: 'Grade Avg',
      cell: (element: LetterDto) => `${element.gradeAvg}`,
      style: (element: LetterDto) => this.getColor(element.gradeAvg),
      color: 'Black',
    },
  ];

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }

  getColor(letter: string): string {
    switch (letter) {
      case "S+":
        return '#FFD700'
      case "S":
        return '#FFD700'
      case "S-":
        return '#FFD700'
      case "A+":
        return '#C9DAF8'
      case "A":
        return '#C9DAF8'
      case "A-":
        return '#C9DAF8'
      case "B+":
        return '#D9D2E9'
      case "B":
        return '#D9D2E9'
      case "B-":
        return '#D9D2E9'
      case "C+":
        return '#B6D7A8'
      case "C":
        return '#B6D7A8'
      case "C-":
        return '#B6D7A8'
      case "D+":
        return '#6FA8DC'
      case "D":
        return '#6FA8DC'
      case "D-":
        return '#6FA8DC'
      case "E+":
        return '#C27BA0'
      case "E":
        return '#C27BA0'
      case "E-":
        return '#C27BA0'
      case "F+":
        return '#A61C00'
      case "F":
        return '#A61C00'
      case "F-":
        return '#A61C00'
      default:
        return ''
    }
  }
}
