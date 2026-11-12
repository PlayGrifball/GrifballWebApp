import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { EMPTY, expand, Observable, switchMap, timer } from 'rxjs';
import { ApiClientService } from '../../api/apiClient.service';
import { HistoricalImportJobDto, HistoricalImportJobSummaryDto, HistoricalImportQueued, HistoricalSeasonReviewDto, HistoricalSourceDto } from '@api/models';
import { problemText } from '../../shared/errorText';
import { VodReviewComponent } from './vodReview/vodReview.component';

/** How often a queued or running job is looked at again. */
export const JOB_POLL_MS = 5000;

/** A finished job is not polled any more. */
export function isActive(job: { status: string }): boolean {
  return job.status === 'Queued' || job.status === 'Running';
}

/** A snapshot's size, "1.2 MB". */
export function size(bytes: number | null): string {
  if (bytes === null)
    return '-';
  if (bytes < 1024)
    return `${bytes} B`;
  if (bytes < 1024 * 1024)
    return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** A TimeSpan ("1.00:00:00", "06:00:00") in words. */
export function interval(timeSpan: string): string {
  const match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(timeSpan);
  if (!match)
    return timeSpan;
  const [days, hours, minutes] = [Number(match[1] ?? 0), Number(match[2]), Number(match[3])];
  const parts = [days && `${days} day${days === 1 ? '' : 's'}`, hours && `${hours} h`, minutes && `${minutes} min`].filter(Boolean);
  return parts.length ? parts.join(' ') : timeSpan;
}

/** What a season's import removes, "3 of 5 stat rows, 1 of 2 teams, 0 of 0 titles". */
export function removalText(r: { statsRemoved: number, statsBefore: number, teamsRemoved: number, teamsBefore: number, titlesRemoved: number, titlesBefore: number }): string {
  return `${r.statsRemoved} of ${r.statsBefore} stat rows, ${r.teamsRemoved} of ${r.teamsBefore} teams, ${r.titlesRemoved} of ${r.titlesBefore} titles`;
}

const KIND_LABELS: Record<string, string> = { Import: 'Import', Refresh: 'Refresh', Preview: 'Preview' };
const REVIEW_LABELS: Record<string, string> = { Preview: 'Would remove', NeedsReview: 'Needs review', Approved: 'Approved', Skipped: 'Skipped' };

/**
 * The historical import (sysadmins): the archive's sources the app downloads itself, refreshing them, previewing and
 * applying an import, the scheduled refresh and its pause, and the seasons an import stopped for review. Every action
 * queues a job the worker runs; the chosen job is shown (and looked at again while it runs).
 */
@Component({
  selector: 'app-historical-import',
  imports: [RouterLink, DatePipe, VodReviewComponent],
  templateUrl: './historicalImport.component.html',
  styleUrl: './historicalImport.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistoricalImportComponent {
  private api = inject(ApiClientService);
  private snackBar = inject(MatSnackBar);

  readonly kindLabels = KIND_LABELS;
  readonly reviewLabels = REVIEW_LABELS;
  readonly size = size;
  readonly interval = interval;
  readonly removalText = removalText;

  readonly sources = rxResource({ params: () => true, stream: () => this.api.getHistoricalSources() });
  readonly sourceRows = computed<HistoricalSourceDto[]>(() => this.sources.hasValue() ? this.sources.value() : []);
  readonly jobs = rxResource({ params: () => true, stream: () => this.api.getHistoricalImportJobs() });
  readonly jobRows = computed<HistoricalImportJobSummaryDto[]>(() => this.jobs.hasValue() ? this.jobs.value() : []);
  readonly schedule = rxResource({ params: () => true, stream: () => this.api.getHistoricalImportSchedule() });

  /** The job shown: looked at again every few seconds until it finishes. */
  readonly selectedJobID = signal<number | null>(null);
  readonly job = rxResource({
    params: () => this.selectedJobID() ?? undefined,
    stream: ({ params }) => this.api.getHistoricalImportJob(params).pipe(
      expand(job => isActive(job) ? timer(JOB_POLL_MS).pipe(switchMap(() => this.api.getHistoricalImportJob(params))) : EMPTY),
    ),
  });
  readonly selectedJob = computed<HistoricalImportJobDto | null>(() => this.job.hasValue() ? this.job.value() : null);
  readonly canApply = computed(() => {
    const job = this.selectedJob();
    return job?.kind === 'Preview' && job.status === 'Succeeded';
  });

  /** An action waiting for its answer: the buttons wait too. */
  readonly busy = signal(false);

  show(jobID: number): void {
    this.selectedJobID.set(jobID);
  }

  refresh(): void {
    this.queue(this.api.refreshHistoricalSources(), "Couldn't start the refresh.");
  }

  preview(): void {
    this.queue(this.api.previewHistoricalImport(), "Couldn't start the preview.");
  }

  apply(): void {
    const job = this.selectedJob();
    if (job)
      this.queue(this.api.applyHistoricalImportPreview(job.jobID), "Couldn't start the import.");
  }

  approve(review: HistoricalSeasonReviewDto): void {
    this.queue(this.api.approveHistoricalSeasonReview(review.reviewID), "Couldn't approve the season.");
  }

  skip(review: HistoricalSeasonReviewDto): void {
    this.run(this.api.skipHistoricalSeasonReview(review.reviewID), "Couldn't skip the season.", () => {
      this.job.reload();
      this.jobs.reload();
    });
  }

  setEnabled(source: HistoricalSourceDto, enabled: boolean): void {
    this.run(this.api.setHistoricalSourceEnabled(source.sourceID, enabled), "Couldn't change the source.", () => this.sources.reload());
  }

  setPaused(paused: boolean): void {
    this.run(this.api.setHistoricalImportSchedulePaused(paused), "Couldn't change the schedule.", () => this.schedule.reload());
  }

  /** Queues a job and shows it. */
  private queue(action: Observable<HistoricalImportQueued>, failed: string): void {
    this.run(action, failed, queued => {
      this.jobs.reload();
      this.show(queued.jobID);
    });
  }

  private run<T>(action: Observable<T>, failed: string, done: (value: T) => void): void {
    this.busy.set(true);
    action.subscribe({
      next: value => {
        this.busy.set(false);
        done(value);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }
}
