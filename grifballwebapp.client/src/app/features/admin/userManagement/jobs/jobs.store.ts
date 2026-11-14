import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal, type ResourceRef } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { JobDto, JobRunDto } from '@api/models';
import { JobsService } from '@api/services/jobs.service';
import { problemText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';
import { Observable, repeat } from 'rxjs';
import { JobKey } from '../../api/jobs';

/** How often the jobs (and the chosen job's runs) are read again. */
export const JOBS_POLL_MS = 10000;

/** An interval in seconds in words: "10 s", "5 min", "1 h 30 min", "1 day". */
export function intervalText(seconds: number | null): string {
  if (seconds === null)
    return '-';
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const rest = seconds % 60;
  const parts = [days && `${days} day${days === 1 ? '' : 's'}`, hours && `${hours} h`, minutes && `${minutes} min`, rest && `${rest} s`].filter(Boolean);
  return parts.length ? parts.join(' ') : '0 s';
}

/**
 * The background jobs and the chosen job's runs, read every few seconds, and the actions on a job (run it, pause it,
 * change or reset its schedule, stop a run). One per page (the component provides it).
 */
@Injectable()
export class JobsStore {
  private readonly api = inject(JobsService);
  private readonly snackBar = inject(MatSnackBar);

  /** The job whose runs are shown (null: none): the component's selection. */
  readonly selectedKey = new StoreInput<JobKey | null>();

  /** Read every few seconds, and again after each action (keeping what's shown meanwhile). */
  private readonly overview = rxResource({
    stream: () => this.api.getJobs().pipe(repeat({ delay: JOBS_POLL_MS })),
  });
  readonly jobs = computed<JobDto[]>(() => this.overview.hasValue() ? this.overview.value().jobs : []);
  readonly nodes = computed(() => this.overview.hasValue() ? this.overview.value().nodes : []);
  readonly loaded = computed(() => this.overview.hasValue());
  readonly failed = computed(() => this.overview.status() === 'error');

  private readonly history = rxResource({
    params: () => this.selectedKey.value() ?? undefined,
    stream: ({ params }) => this.api.getJobHistory({ group: params.group, name: params.name, take: 50 }).pipe(repeat({ delay: JOBS_POLL_MS })),
  });
  /** The chosen job's last runs; null until they are read. */
  readonly runs = computed<JobRunDto[] | null>(() => this.history.hasValue() ? this.history.value() : null);
  readonly runsFailed = computed(() => this.history.status() === 'error');

  /** An action waiting for its answer: the buttons wait too. */
  private readonly _busy = signal(false);
  readonly busy = this._busy.asReadonly();

  constructor() {
    this.readAgainAfterAnError(this.overview);
    this.readAgainAfterAnError(this.history);
  }

  run(job: JobDto): void {
    this.act(this.api.runJob({ group: job.group, name: job.name }), `${job.displayName} will run in a few seconds.`, "Couldn't run the job.");
  }

  setPaused(job: JobDto, paused: boolean): void {
    this.act(this.api.setJobPaused({ group: job.group, name: job.name, body: { paused } }), paused ? `${job.displayName} is paused.` : `${job.displayName} is resumed.`, "Couldn't change the job.");
  }

  setInterval(job: JobDto, seconds: number): void {
    this.act(this.api.setJobInterval({ group: job.group, name: job.name, body: { intervalSeconds: seconds } }), `${job.displayName} runs every ${intervalText(seconds)}.`, "Couldn't change the schedule.");
  }

  /** Puts a job back on its default schedule; `done` runs once it is. */
  resetSchedule(job: JobDto, done: () => void): void {
    this.act(this.api.resetJobSchedule({ group: job.group, name: job.name }), `${job.displayName} is back on its default schedule.`, "Couldn't reset the schedule.", done);
  }

  interrupt(fireInstanceID: string): void {
    this.act(this.api.interruptJob({ fireInstanceID }), 'Asked the run to stop.', "Couldn't stop the run.");
  }

  /** A failed read ends its polling (repeat only reads again after a success): read again a poll later, so one blip doesn't leave the page stale until it's reloaded. */
  private readAgainAfterAnError<T>(resource: ResourceRef<T>): void {
    effect(onCleanup => {
      if (resource.status() !== 'error')
        return;
      const timeout = setTimeout(() => resource.reload(), JOBS_POLL_MS);
      onCleanup(() => clearTimeout(timeout));
    });
  }

  private act(action: Observable<void>, done: string, failed: string, then?: () => void): void {
    this._busy.set(true);
    action.subscribe({
      next: () => {
        this._busy.set(false);
        this.snackBar.open(done, 'Close', { duration: 4000 });
        this.overview.reload();
        this.history.reload();
        then?.();
      },
      error: (error: HttpErrorResponse) => {
        this._busy.set(false);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }
}
