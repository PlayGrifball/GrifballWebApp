import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { HistoricalImportJobSummaryDto, HistoricalImportQueued, HistoricalSourceDto } from '@api/models';
import { HistoricalImportService } from '@api/services/historical-import.service';
import { problemText } from '@shared/errorText';
import { EMPTY, expand, Observable, switchMap, timer } from 'rxjs';
import { resourceView } from '@shared/resourceView';

/** How often a queued or running job is looked at again. */
export const JOB_POLL_MS = 5000;

/** A finished job is not polled any more. */
export function isActive(job: { status: string }): boolean {
  return job.status === 'Queued' || job.status === 'Running';
}

/**
 * The historical import: its sources, jobs and schedule, the job shown (looked at again while it runs), and the actions
 * that queue jobs or change a source or the schedule. One per page (the component provides it).
 */
@Injectable()
export class HistoricalImportStore {
  private readonly api = inject(HistoricalImportService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly sources = resourceView(rxResource({ params: () => true, stream: () => this.api.getHistoricalSources() }), [] as HistoricalSourceDto[]);
  readonly sourceRows = this.sources.value;
  readonly sourcesFailed = this.sources.failed;
  private readonly jobs = resourceView(rxResource({ params: () => true, stream: () => this.api.getHistoricalImportJobs() }), [] as HistoricalImportJobSummaryDto[]);
  readonly jobRows = this.jobs.value;
  readonly jobsLoaded = this.jobs.hasValue;
  readonly jobsFailed = this.jobs.failed;
  private readonly schedule = resourceView(rxResource({ params: () => true, stream: () => this.api.getHistoricalImportSchedule() }), null);
  readonly scheduleValue = this.schedule.value;
  readonly scheduleFailed = this.schedule.failed;

  /** The job shown: looked at again every few seconds until it finishes. */
  private readonly _selectedJobID = signal<number | null>(null);
  readonly selectedJobID = this._selectedJobID.asReadonly();
  private readonly job = resourceView(rxResource({
    params: () => this.selectedJobID() ?? undefined,
    stream: ({ params }) => this.api.getHistoricalImportJob({ jobID: params }).pipe(
      expand(job => isActive(job) ? timer(JOB_POLL_MS).pipe(switchMap(() => this.api.getHistoricalImportJob({ jobID: params }))) : EMPTY),
    ),
  }), null);
  readonly selectedJob = this.job.value;

  /** An action waiting for its answer: the buttons wait too. */
  private readonly _busy = signal(false);
  readonly busy = this._busy.asReadonly();

  show(jobID: number): void {
    this._selectedJobID.set(jobID);
  }

  refresh(): void {
    this.queue(this.api.refreshHistoricalSources(), "Couldn't start the refresh.");
  }

  preview(): void {
    this.queue(this.api.previewHistoricalImport(), "Couldn't start the preview.");
  }

  /** Applies the preview job's import. */
  apply(jobID: number): void {
    this.queue(this.api.applyHistoricalImportPreview({ jobID }), "Couldn't start the import.");
  }

  approve(reviewID: number): void {
    this.queue(this.api.approveHistoricalSeasonReview({ reviewID }), "Couldn't approve the season.");
  }

  skip(reviewID: number): void {
    this.run(this.api.skipHistoricalSeasonReview({ reviewID }), "Couldn't skip the season.", () => {
      this.job.reload();
      this.jobs.reload();
    });
  }

  setEnabled(sourceID: HistoricalSourceDto['sourceID'], enabled: boolean): void {
    this.run(this.api.setHistoricalSourceEnabled({ sourceID, body: { enabled } }), "Couldn't change the source.", () => this.sources.reload());
  }

  setPaused(paused: boolean): void {
    this.run(this.api.setHistoricalImportSchedulePaused({ body: { paused } }), "Couldn't change the schedule.", () => this.schedule.reload());
  }

  /** Queues a job and shows it. */
  private queue(action: Observable<HistoricalImportQueued>, failed: string): void {
    this.run(action, failed, queued => {
      this.jobs.reload();
      this.show(queued.jobID);
    });
  }

  private run<T>(action: Observable<T>, failed: string, done: (value: T) => void): void {
    this._busy.set(true);
    action.subscribe({
      next: value => {
        this._busy.set(false);
        done(value);
      },
      error: (error: HttpErrorResponse) => {
        this._busy.set(false);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }
}
