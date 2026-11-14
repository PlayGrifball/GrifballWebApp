import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { HistoricalSeasonReviewDto, HistoricalSourceDto } from '@api/models';
import { VodReviewComponent } from './vodReview/vodReview.component';
import { HistoricalImportStore } from './historicalImport.store';

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
  providers: [HistoricalImportStore],
})
export class HistoricalImportComponent {
  private readonly store = inject(HistoricalImportStore);

  readonly kindLabels = KIND_LABELS;
  readonly reviewLabels = REVIEW_LABELS;
  readonly size = size;
  readonly interval = interval;
  readonly removalText = removalText;

  readonly sourceRows = this.store.sourceRows;
  readonly sourcesFailed = this.store.sourcesFailed;
  readonly jobRows = this.store.jobRows;
  readonly jobsLoaded = this.store.jobsLoaded;
  readonly jobsFailed = this.store.jobsFailed;
  readonly scheduleValue = this.store.scheduleValue;
  readonly scheduleFailed = this.store.scheduleFailed;

  /** The job shown: looked at again every few seconds until it finishes. */
  readonly selectedJobID = this.store.selectedJobID;
  readonly selectedJob = this.store.selectedJob;
  readonly canApply = computed(() => {
    const job = this.selectedJob();
    return job?.kind === 'Preview' && job.status === 'Succeeded';
  });

  /** An action waiting for its answer: the buttons wait too. */
  readonly busy = this.store.busy;

  show(jobID: number): void {
    this.store.show(jobID);
  }

  refresh(): void {
    this.store.refresh();
  }

  preview(): void {
    this.store.preview();
  }

  apply(): void {
    const job = this.selectedJob();
    if (job)
      this.store.apply(job.jobID);
  }

  approve(review: HistoricalSeasonReviewDto): void {
    this.store.approve(review.reviewID);
  }

  skip(review: HistoricalSeasonReviewDto): void {
    this.store.skip(review.reviewID);
  }

  setEnabled(source: HistoricalSourceDto, enabled: boolean): void {
    this.store.setEnabled(source.sourceID, enabled);
  }

  setPaused(paused: boolean): void {
    this.store.setPaused(paused);
  }
}
