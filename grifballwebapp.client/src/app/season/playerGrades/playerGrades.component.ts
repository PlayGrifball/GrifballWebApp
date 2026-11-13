import { DecimalPipe } from '@angular/common';
import { Component, ChangeDetectionStrategy, computed, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { LetterDto } from '@api/models';
import { MatTableModule } from '@angular/material/table';
import { SeasonTabsComponent } from '../seasonTabs/seasonTabs.component';
import { DataNoticeComponent } from '../../shared/dataNotice/dataNotice.component';
import { missing, SeasonSummaryService } from '../seasonSummary';
import type { SeasonSummaryDto } from '@api/models';
import { whileCurrent } from '../../shared/whileCurrent';
import { GradesService } from '@api/services/grades.service';

@Component({
    selector: 'app-player-grades',
    imports: [
        SeasonTabsComponent,
        MatTableModule,
        DataNoticeComponent,
        RouterLink,
    ],
    providers: [DecimalPipe],
    templateUrl: './playerGrades.component.html',
    styleUrl: './playerGrades.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PlayerGradesComponent {
  private grades = inject(GradesService);
  private pipe = inject(DecimalPipe);
  private summaries = inject(SeasonSummaryService);

  /** The :seasonID of the route. Angular keeps this page when only it changes, so a new one loads that season's grades. */
  readonly seasonID = input.required({ transform: numberAttribute });

  readonly letters = signal<LetterDto[]>([]);
  readonly summary = signal<SeasonSummaryDto | null>(null);
  /** The summary has arrived, or failed (the grades are then shown without it). */
  private readonly summaryDone = signal(false);
  /** The grades have arrived, or failed. */
  readonly gradesDone = signal(false);
  readonly gradesFailed = signal(false);
  /**
   * Grades come from the season's Halo Infinite games: a backfilled or ended season without any says so in place of the
   * table. No scheduled matches settles it without asking for the grades; a schedule without any Waypoint games pulled
   * shows up as no grades.
   */
  readonly noMatches = computed(() =>
    missing(this.summary(), s => s.matchCount + s.bracketMatchCount)
    || (this.gradesDone() && !this.gradesFailed() && missing(this.summary(), () => this.letters().length)));
  /** Nothing is drawn until the page knows whether it shows the notice or the table, so neither flashes up first. */
  readonly showTable = computed(() => this.summaryDone() && this.gradesDone() && !this.noMatches());

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
    effect(() => {
      this.seasonID();
      untracked(() => this.load());
    });
  }

  private load(): void {
    // Nothing of the previous season stays
    this.letters.set([]);
    this.summary.set(null);
    this.summaryDone.set(false);
    this.gradesDone.set(false);
    this.gradesFailed.set(false);
    const seasonID = this.seasonID();
    this.summaries.get(seasonID).pipe(whileCurrent(seasonID, this.seasonID)).subscribe({
      next: s => {
        this.summary.set(s);
        this.summaryDone.set(true);
        // A season without any matches only shows the notice: the grouped grades query isn't worth running
        if (!this.noMatches()) this.getGrades();
      },
      error: () => {
        this.summaryDone.set(true);
        this.getGrades();
      },
    });
  }

  getGrades(): void {
    const seasonID = this.seasonID();
    this.grades.getGrades({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID))
      .subscribe({
        next: result => {
          this.letters.set(result.letters);
          this.gradesDone.set(true);
        },
        error: () => {
          this.gradesFailed.set(true);
          this.gradesDone.set(true);
        },
      });
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
