import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { ScheduleRecommendationMatchDto, ScheduleRecommendationPageDto, UnscheduledMatchDto } from '@api/models';
import { MatchPlannerService } from '@api/services/match-planner.service';
import { SchedulerService } from '@api/services/scheduler.service';
import { toApiDate } from '@shared/apiDates';
import { errorText } from '@shared/errorText';
import { whileCurrent } from '@shared/whileCurrent';
import { DateTime } from 'luxon';
import { map, Subscription, switchMap, takeWhile, timer } from 'rxjs';
import { parseScheduledMatch } from './scheduledMatchDto';
import { scheduleWeeks, WeekDto } from './weekDto';

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

/**
 * A season's schedule: its matches with a time (by week) and without, the times set from it, and the schedule
 * recommendation (a run of the genetic algorithm on the worker, watched until done, and applied). One per schedule
 * (the component provides it).
 */
@Injectable()
export class ScheduleListStore {
  private readonly matchPlanner = inject(MatchPlannerService);
  private readonly scheduler = inject(SchedulerService);
  private readonly destroyRef = inject(DestroyRef);

  /** The season shown (0: none yet), set by {@link show}. */
  private readonly seasonID = signal(0);

  private readonly _unscheduledMatches = signal<UnscheduledMatchDto[]>([]);
  readonly unscheduledMatches = this._unscheduledMatches.asReadonly();
  private readonly _scheduledMatches = signal<WeekDto[]>([]);
  readonly scheduledMatches = this._scheduledMatches.asReadonly();
  /** Both lists have loaded (or failed to): shown then together, so the unscheduled list does
   * not jump down when the weeks above it arrive. */
  private readonly scheduledLoaded = signal(false);
  private readonly unscheduledLoaded = signal(false);
  readonly loaded = computed(() => this.scheduledLoaded() && this.unscheduledLoaded());

  /** The schedule recommendation: its run on the worker and the latest one; null until read. */
  private readonly _recommendation = signal<ScheduleRecommendationPageDto | null>(null);
  readonly recommendation = this._recommendation.asReadonly();
  readonly recommending = computed(() => ['Queued', 'Running'].includes(this.recommendation()?.run.state ?? 'Idle'));
  private readonly _recommendationError = signal<string | null>(null);
  readonly recommendationError = this._recommendationError.asReadonly();
  /** The matches applying would set: those without a result. */
  readonly applicable = computed(() => this.recommendation()?.latest?.matches.filter(m => !m.decided).length ?? 0);
  readonly conflicts = computed(() => this.recommendation()?.latest?.matches.filter(m => conflictText(m) !== '').length ?? 0);
  private recommendationWatch?: Subscription;

  /** Shows a season's schedule (0: none). Nothing of the previous season's stays, nor its recommendation. */
  show(seasonID: number): void {
    this.seasonID.set(seasonID);
    this.recommendationWatch?.unsubscribe();
    this._unscheduledMatches.set([]);
    this._scheduledMatches.set([]);
    this.unscheduledLoaded.set(false);
    this.scheduledLoaded.set(false);
    this._recommendation.set(null);
    this._recommendationError.set(null);
    if (seasonID !== 0)
      this.reload();
  }

  /** Loads both lists again. */
  reload(): void {
    const seasonID = this.seasonID();
    this.matchPlanner.getUnscheduledMatches({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID))
      .subscribe({
        next: r => {
          this._unscheduledMatches.set(r);
          this.unscheduledLoaded.set(true);
        },
        error: () => this.unscheduledLoaded.set(true),
      });
    this.matchPlanner.getScheduledMatches({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID), map(matches => matches.map(parseScheduledMatch)))
      .subscribe({
        next: r => {
          this._scheduledMatches.set(scheduleWeeks(r));
          this.scheduledLoaded.set(true);
        },
        error: () => this.scheduledLoaded.set(true),
      });
  }

  /** Sets a match's time (none: no time), then loads the schedule again, whatever the answer. */
  setTime(seasonMatchID: number, time: DateTime | null): void {
    this.matchPlanner.updateMatchTime({ seasonMatchID, body: { seasonMatchID, time: toApiDate(time) } })
      .subscribe({
        next: () => this.reload(),
        error: () => this.reload(),
      });
  }

  /** The latest recommendation and where its run is; one queued or running is watched until it's done. */
  loadRecommendation(): void {
    const seasonID = this.seasonID();
    this.scheduler.getScheduleRecommendation({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: page => this.showRecommendation(page),
        error: (error: HttpErrorResponse) => this._recommendationError.set(errorText(error, "Couldn't load the recommended times.")),
      });
  }

  /** Queues a run of the genetic algorithm for the season, on the worker. */
  recommend(): void {
    this._recommendationError.set(null);
    const seasonID = this.seasonID();
    this.scheduler.recommendSchedule({ seasonID })
      .pipe(whileCurrent(seasonID, this.seasonID), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadRecommendation(),
        error: (error: HttpErrorResponse) => this._recommendationError.set(errorText(error, "Couldn't start the recommendation.")),
      });
  }

  /** Gives each match without a result its recommended time, then reads the schedule and the recommendation again. */
  apply(): void {
    const latest = this.recommendation()?.latest;
    if (!latest)
      return;
    this.scheduler.applyScheduleRecommendation({ recommendationID: latest.scheduleRecommendationID })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.reload();
          this.loadRecommendation();
        },
        error: (error: HttpErrorResponse) => this._recommendationError.set(errorText(error, "Couldn't apply the recommended times.")),
      });
  }

  private showRecommendation(page: ScheduleRecommendationPageDto): void {
    this._recommendation.set(page);
    if (page.run.state === 'Idle')
      return;
    this.recommendationWatch?.unsubscribe();
    this.recommendationWatch = timer(RECOMMEND_POLL_MS, RECOMMEND_POLL_MS).pipe(
      switchMap(() => this.scheduler.getScheduleRecommendation({ seasonID: this.seasonID() })),
      takeWhile(next => next.run.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => this._recommendation.set(next),
      error: () => this._recommendationError.set("Couldn't tell whether the recommendation finished. Close and reopen the editor to check."),
    });
  }
}
