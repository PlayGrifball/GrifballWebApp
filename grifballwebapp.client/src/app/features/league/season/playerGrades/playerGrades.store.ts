import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import type { LetterDto, SeasonSummaryDto } from '@api/models';
import { GradesService } from '@api/services/grades.service';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import { missing, SeasonSummaryService } from '../seasonSummary';

/** A season's player grades and its summary. One per page (the component provides it). */
@Injectable()
export class PlayerGradesStore {
  private readonly grades = inject(GradesService);
  private readonly summaries = inject(SeasonSummaryService);

  /** The route's season: the component's input. */
  readonly seasonID = new StoreInput<number>();

  private readonly _letters = signal<LetterDto[]>([]);
  readonly letters = this._letters.asReadonly();
  private readonly _summary = signal<SeasonSummaryDto | null>(null);
  readonly summary = this._summary.asReadonly();
  /** The summary has arrived, or failed (the grades are then shown without it). */
  private readonly _summaryDone = signal(false);
  readonly summaryDone = this._summaryDone.asReadonly();
  /** The grades have arrived, or failed. */
  private readonly _gradesDone = signal(false);
  readonly gradesDone = this._gradesDone.asReadonly();
  private readonly _gradesFailed = signal(false);
  readonly gradesFailed = this._gradesFailed.asReadonly();
  /**
   * Grades come from the season's Halo Infinite games: a backfilled or ended season without any has none. No scheduled
   * matches settles it without asking for the grades; a schedule without any Waypoint games pulled shows up as no grades.
   */
  readonly noMatches = computed(() =>
    missing(this.summary(), s => s.matchCount + s.bracketMatchCount)
    || (this.gradesDone() && !this.gradesFailed() && missing(this.summary(), () => this.letters().length)));

  constructor() {
    effect(() => {
      const seasonID = this.seasonID.value();
      if (seasonID !== undefined)
        untracked(() => this.load(seasonID));
    });
  }

  private load(seasonID: number): void {
    // Nothing of the previous season stays
    this._letters.set([]);
    this._summary.set(null);
    this._summaryDone.set(false);
    this._gradesDone.set(false);
    this._gradesFailed.set(false);
    this.summaries.get(seasonID).pipe(whileCurrent(seasonID, this.seasonID.value)).subscribe({
      next: s => {
        this._summary.set(s);
        this._summaryDone.set(true);
        // A season without any matches only shows the notice: the grouped grades query isn't worth running
        if (!this.noMatches()) this.getGrades(seasonID);
      },
      error: () => {
        this._summaryDone.set(true);
        this.getGrades(seasonID);
      },
    });
  }

  private getGrades(seasonID: number): void {
    this.grades.getGrades({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID.value))
      .subscribe({
        next: result => {
          this._letters.set(result.letters);
          this._gradesDone.set(true);
        },
        error: () => {
          this._gradesFailed.set(true);
          this._gradesDone.set(true);
        },
      });
  }
}
