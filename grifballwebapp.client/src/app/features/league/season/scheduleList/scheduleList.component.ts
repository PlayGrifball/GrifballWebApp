import { CommonModule } from '@angular/common';
import { Component, ChangeDetectionStrategy, effect, inject, input, signal, untracked, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { RouterModule } from '@angular/router';
import type { UnscheduledMatchDto } from '@api/models';
import { MatDividerModule } from '@angular/material/divider';
import { AccountService } from '@core/auth/account.service';
import { ErrorMessageComponent } from "@shared/validation/errorMessage.component";
import { MatFormFieldModule } from '@angular/material/form-field';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { DateTime } from 'luxon';
import { WeekGameDto } from './weekDto';
import { CreateRegularMatchesDialogComponent } from './createRegularMatchesDialog/createRegularMatchesDialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { ToDatePipe } from '@shared/toDate.pipe';
import { provideDatePickers } from '@shared/datePickers';
import { conflictText, ScheduleListStore } from './scheduleList.store';

@Component({
    selector: 'app-schedule-list',
    templateUrl: './scheduleList.component.html',
    styleUrl: './scheduleList.component.scss',
    imports: [
        CommonModule,
        MatCardModule,
        MatDividerModule,
        ErrorMessageComponent,
        MatFormFieldModule,
        MtxDatetimepickerModule,
        MatInputModule,
        FormsModule,
        MatButtonModule,
        RouterModule,
        MatDialogModule,
        ToDatePipe,
    ],
    providers: [provideDatePickers(), ScheduleListStore],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScheduleListComponent {
  /** The season's schedule and recommendation, and the calls that change them. */
  private readonly store = inject(ScheduleListStore);
  accountService = inject(AccountService);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  /** The season page's season (0: none yet). A new one loads its schedule in place of the one shown. */
  readonly seasonID = input.required<number>();
  readonly unscheduledMatches = this.store.unscheduledMatches;
  readonly scheduledMatches = this.store.scheduledMatches;
  readonly isEditing = signal(false);
  /** Both lists have loaded (or failed to): shown together. */
  readonly loaded = this.store.loaded;

  /** The schedule recommendation (organizers, while editing): its run on the worker and the latest one; null until read. */
  readonly recommendation = this.store.recommendation;
  readonly recommending = this.store.recommending;
  readonly recommendationError = this.store.recommendationError;
  /** Apply was pressed: the times are set once it's confirmed. */
  readonly confirmingApply = signal(false);
  /** The matches applying would set: those without a result. */
  readonly applicable = this.store.applicable;
  readonly conflicts = this.store.conflicts;
  readonly conflictText = conflictText;

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      untracked(() => {
        // Nothing of the previous season's schedule stays, nor its editor or recommendation
        this.isEditing.set(false);
        this.confirmingApply.set(false);
        this.store.show(seasonID);
      });
    });
  }

  createRegularSeasonMatches() {
    const dialogRef = this.dialog.open(CreateRegularMatchesDialogComponent, {
      data: this.seasonID()
    });

    dialogRef.componentInstance.regularMatchesCreated
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => {
      this.store.reload();
    });
  }

  toggleEdit(): void {
    this.isEditing.update(editing => !editing);
    if (this.isEditing() && this.accountService.isEventOrganizer())
      this.loadRecommendation();
  }

  /** The latest recommendation and where its run is; one queued or running is watched until it's done. */
  loadRecommendation(): void {
    this.store.loadRecommendation();
  }

  /** Queues a run of the genetic algorithm for the season, on the worker. */
  recommend(): void {
    this.confirmingApply.set(false);
    this.store.recommend();
  }

  /** Gives each match without a result its recommended time (after the confirmation), then reads the schedule again. */
  apply(): void {
    if (!this.recommendation()?.latest)
      return;
    this.confirmingApply.set(false);
    this.store.apply();
  }

  /** `time` is the row's own form value; the loaded match objects are never edited. */
  onSubmit(match: UnscheduledMatchDto | WeekGameDto, time: DateTime | '' | null | undefined): void {
    // An empty picker gives '', which the server's DateTime? cannot bind: send null (no time).
    this.store.setTime(match.seasonMatchID, time || null);
  }
}
