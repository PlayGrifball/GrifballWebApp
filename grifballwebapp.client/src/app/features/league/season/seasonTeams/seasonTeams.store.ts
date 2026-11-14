import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { SeasonSummaryDto, SeasonTeamDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { catchError, EMPTY, merge, Subject, switchMap, tap } from 'rxjs';
import { SeasonSummaryService } from '../seasonSummary';

/** A season's teams with their rosters, and its summary. One per page (the component provides it). */
@Injectable()
export class SeasonTeamsStore {
  private readonly api = inject(SeasonService);
  private readonly summaries = inject(SeasonSummaryService);

  private readonly _seasonID = signal(0);
  readonly seasonID = this._seasonID.asReadonly();
  private readonly _summary = signal<SeasonSummaryDto | null>(null);
  readonly summary = this._summary.asReadonly();
  private readonly _teams = signal<SeasonTeamDto[]>([]);
  readonly teams = this._teams.asReadonly();
  private readonly _loading = signal(true);
  readonly loading = this._loading.asReadonly();
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  /** The season to load; switchMap drops the previous season's responses when another one is asked for. */
  private readonly load = new Subject<number>();

  constructor() {
    this.load.pipe(
      switchMap(seasonID => merge(
        // The summary only adds the banner and the draft shortcut: the rosters show without it
        this.summaries.get(seasonID).pipe(
          tap(s => this._summary.set(s)),
          catchError(() => EMPTY),
        ),
        this.api.getSeasonTeams({ seasonID }).pipe(
          tap(teams => {
            this._teams.set(teams);
            this._loading.set(false);
          }),
          catchError((err: HttpErrorResponse) => {
            this._error.set(err.status === 404 ? 'Season not found' : 'Failed to load season teams');
            this._loading.set(false);
            return EMPTY;
          }),
        ),
      )),
      takeUntilDestroyed(),
    ).subscribe();
  }

  /** Loads a season, forgetting the one shown before so none of it is left on the new season's page. */
  show(seasonID: number): void {
    this._seasonID.set(seasonID);
    this._summary.set(null);
    this._teams.set([]);
    this._loading.set(true);
    this._error.set(null);
    this.load.next(seasonID);
  }
}
