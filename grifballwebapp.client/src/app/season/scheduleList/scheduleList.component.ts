import { CommonModule } from '@angular/common';
import { Component, ChangeDetectionStrategy, computed, effect, inject, input, signal, untracked, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import type { ScheduleRecommendationMatchDto, ScheduleRecommendationPageDto, UnscheduledMatchDto, UpdateMatchTimeDto } from '@api/models';
import { MatDividerModule } from '@angular/material/divider';
import { AccountService } from '../../account.service';
import { ErrorMessageComponent } from "../../validation/errorMessage.component";
import { MatFormFieldModule } from '@angular/material/form-field';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { parseScheduledMatch, ScheduledMatchDto } from './scheduledMatchDto';
import { DateTime } from 'luxon';
import { toApiDate } from '../../shared/apiDates';
import { TimeDto } from './timeDto';
import { groupBy, map, orderBy } from 'lodash-es';
import { map as mapResponse, Subscription, switchMap, takeWhile, timer } from 'rxjs';
import { errorText } from '../../shared/errorText';
import { WeekDto, WeekGameDto } from './weekDto';
import { CreateRegularMatchesDialogComponent } from './createRegularMatchesDialog/createRegularMatchesDialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { ToDatePipe } from '../../shared/toDate.pipe';
import { provideDatePickers } from '../../shared/datePickers';
import { whileCurrent } from '../../shared/whileCurrent';
import { MatchPlannerService } from '@api/services/match-planner.service';
import { SchedulerService } from '@api/services/scheduler.service';

/** How often a queued or running schedule recommendation is looked at again. */
export const RECOMMEND_POLL_MS = 3000;

/** What is wrong with a recommended time: an overlap, a team that isn't available then; empty when nothing is. */
export function conflictText(match: ScheduleRecommendationMatchDto): string {
  const problems = [
    match.overlaps ? 'Overlaps another match' : '',
    match.homeTeamAvailable ? '' : `${match.homeTeam} unavailable`,
    match.awayTeamAvailable ? '' : `${match.awayTeam} unavailable`,
  ];
  return problems.filter(Boolean).join(', ');
}

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
    providers: [provideDatePickers()],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScheduleListComponent {
  private matchPlanner = inject(MatchPlannerService);
  private scheduler = inject(SchedulerService);
  accountService = inject(AccountService);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  /** The season page's season (0: none yet). A new one loads its schedule in place of the one shown. */
  readonly seasonID = input.required<number>();
  readonly unscheduledMatches = signal<UnscheduledMatchDto[]>([]);
  readonly scheduledMatches = signal<WeekDto[]>([]);
  readonly isEditing = signal(false);
  /** Both lists have loaded (or failed to): shown then together, so the unscheduled list does
   * not jump down when the weeks above it arrive. */
  private readonly scheduledLoaded = signal(false);
  private readonly unscheduledLoaded = signal(false);
  readonly loaded = computed(() => this.scheduledLoaded() && this.unscheduledLoaded());

  /** The schedule recommendation (organizers, while editing): its run on the worker and the latest one; null until read. */
  readonly recommendation = signal<ScheduleRecommendationPageDto | null>(null);
  readonly recommending = computed(() => ['Queued', 'Running'].includes(this.recommendation()?.run.state ?? 'Idle'));
  readonly recommendationError = signal<string | null>(null);
  /** Apply was pressed: the times are set once it's confirmed. */
  readonly confirmingApply = signal(false);
  /** The matches applying would set: those without a result. */
  readonly applicable = computed(() => this.recommendation()?.latest?.matches.filter(m => !m.decided).length ?? 0);
  readonly conflicts = computed(() => this.recommendation()?.latest?.matches.filter(m => conflictText(m) !== '').length ?? 0);
  readonly conflictText = conflictText;
  private recommendationWatch?: Subscription;

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      untracked(() => {
        // Nothing of the previous season's schedule stays, nor its editor or recommendation
        this.recommendationWatch?.unsubscribe();
        this.unscheduledMatches.set([]);
        this.scheduledMatches.set([]);
        this.unscheduledLoaded.set(false);
        this.scheduledLoaded.set(false);
        this.isEditing.set(false);
        this.recommendation.set(null);
        this.recommendationError.set(null);
        this.confirmingApply.set(false);
        if (seasonID !== 0)
          this.GetMatches();
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
      this.GetMatches();
    });
  }

  private GetMatches(): void {
    this.GetUnscheduledMatches();
    this.GetScheduledMatches();
  }

  GetUnscheduledMatches(): void {
    const seasonID = this.seasonID();
    this.matchPlanner.getUnscheduledMatches({ seasonID })
    .pipe(whileCurrent(seasonID, this.seasonID))
    .subscribe({
      next: r => {
        this.unscheduledMatches.set(r);
        this.unscheduledLoaded.set(true);
      },
      error: () => this.unscheduledLoaded.set(true),
    });
  }

  GetScheduledMatches(): void {
    const seasonID = this.seasonID();
    this.matchPlanner.getScheduledMatches({ seasonID })
    .pipe(whileCurrent(seasonID, this.seasonID), mapResponse(matches => matches.map(parseScheduledMatch)))
    .subscribe({
      next: r => {
        this.gotScheduledMatches(r);
        this.scheduledLoaded.set(true);
      },
      error: () => this.scheduledLoaded.set(true),
    });
  }

  private gotScheduledMatches(matches: ScheduledMatchDto[]): void {
    const mappedMatches = matches.map(match => this.map(match));

    const grouped = groupBy(mappedMatches, (match) => match.LocalWeekYear + "-" + match.LocalWeekNumber);
    const mapped = map(grouped, (matches, key) => ({ key, matches }));
    // Sort by year then week number (not Number(key): "2026-5" is NaN), so weeks
    // stay chronological across a year boundary (week 52 of 2026 before week 1 of 2027).
    const groupedByWeek = orderBy(mapped, [group => group.matches[0].LocalWeekYear, group => group.matches[0].LocalWeekNumber], ['asc', 'asc']);

    let weeks: WeekDto[] = [];

    let weekNumber = 1;
    groupedByWeek.forEach((x, index) => {
        const week = x.matches;

        const weekDto: WeekDto = {
          weekNumber: weekNumber++,
          games: week.map(match => this.mapTimeDtoToWeekGameDto(match))
        };
        weeks.push(weekDto);
    });

    this.scheduledMatches.set(weeks);
  }

  private map(value: ScheduledMatchDto): TimeDto {
    const dateTime = value.time;

    return {
      SeasonMatchID: value.seasonMatchID,
      HomeTeamName: value.homeTeam,
      AwayTeamName: value.awayTeam,
      Complete: value.complete,
      Time: value.time,
      DateTime: dateTime,
      LocalWeekNumber: dateTime.localWeekNumber,
      LocalWeekYear: dateTime.localWeekYear,
    }
  }

  private mapTimeDtoToWeekGameDto(value: TimeDto): WeekGameDto {
    return {
      seasonMatchID: value.SeasonMatchID,
      homeTeamName: value.HomeTeamName,
      awayTeamName: value.AwayTeamName,
      complete: value.Complete,
      time: value.DateTime,
    }
  }

  toggleEdit(): void {
    this.isEditing.update(editing => !editing);
    if (this.isEditing() && this.accountService.isEventOrganizer())
      this.loadRecommendation();
  }

  /** The latest recommendation and where its run is; one queued or running is watched until it's done. */
  loadRecommendation(): void {
    const seasonID = this.seasonID();
    this.scheduler.getScheduleRecommendation({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: page => this.showRecommendation(page),
        error: (error: HttpErrorResponse) => this.recommendationError.set(errorText(error, "Couldn't load the recommended times.")),
      });
  }

  /** Queues a run of the genetic algorithm for the season, on the worker. */
  recommend(): void {
    this.recommendationError.set(null);
    this.confirmingApply.set(false);
    const seasonID = this.seasonID();
    this.scheduler.recommendSchedule({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadRecommendation(),
        error: (error: HttpErrorResponse) => this.recommendationError.set(errorText(error, "Couldn't start the recommendation.")),
      });
  }

  /** Gives each match without a result its recommended time (after the confirmation), then reads the schedule again. */
  apply(): void {
    const latest = this.recommendation()?.latest;
    if (!latest)
      return;
    this.confirmingApply.set(false);
    this.scheduler.applyScheduleRecommendation({ recommendationID: latest.scheduleRecommendationID })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.GetMatches();
          this.loadRecommendation();
        },
        error: (error: HttpErrorResponse) => this.recommendationError.set(errorText(error, "Couldn't apply the recommended times.")),
      });
  }

  private showRecommendation(page: ScheduleRecommendationPageDto): void {
    this.recommendation.set(page);
    if (page.run.state === 'Idle')
      return;
    this.recommendationWatch?.unsubscribe();
    this.recommendationWatch = timer(RECOMMEND_POLL_MS, RECOMMEND_POLL_MS).pipe(
      switchMap(() => this.scheduler.getScheduleRecommendation({ seasonID: this.seasonID() })),
      takeWhile(next => next.run.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => this.recommendation.set(next),
      error: () => this.recommendationError.set("Couldn't tell whether the recommendation finished. Close and reopen the editor to check."),
    });
  }

  /** `time` is the row's own form value; the loaded match objects are never edited. */
  onSubmit(match: UnscheduledMatchDto | WeekGameDto, time: DateTime | '' | null | undefined): void {
    // An empty picker gives '', which the server's DateTime? cannot bind: send null (no time).
    const dto: UpdateMatchTimeDto = { seasonMatchID: match.seasonMatchID, time: toApiDate(time || null) };
    this.matchPlanner.updateMatchTime({ seasonMatchID: dto.seasonMatchID, body: dto })
    .subscribe({
      next: r => this.GetMatches(),
      error: e => this.GetMatches()
    })
  }
}

