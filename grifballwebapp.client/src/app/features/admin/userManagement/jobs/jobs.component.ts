import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, type ResourceRef } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { Observable, repeat } from 'rxjs';
import { JobsService } from '@api/services/jobs.service';
import type { JobDto } from '@api/models';
import { JobKey, JobState } from '../../api/jobs';
import { problemText } from '../../../../shared/errorText';

/** How often the jobs (and the chosen job's runs) are read again. */
export const JOBS_POLL_MS = 10000;

export type IntervalUnit = 'seconds' | 'minutes' | 'hours' | 'days';

export const UNIT_SECONDS: Record<IntervalUnit, number> = { seconds: 1, minutes: 60, hours: 3600, days: 86400 };

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

/** The largest unit an interval is a whole number of, for the editor: 3600 is 1 hours, 90 is 90 seconds. */
export function splitInterval(seconds: number): { value: number, unit: IntervalUnit } {
  for (const unit of ['days', 'hours', 'minutes'] as const) {
    if (seconds >= UNIT_SECONDS[unit] && seconds % UNIT_SECONDS[unit] === 0)
      return { value: seconds / UNIT_SECONDS[unit], unit };
  }
  return { value: seconds, unit: 'seconds' };
}

/** A run's duration: "12 ms", "3.4 s", "2 min 5 s". */
export function durationText(ms: number): string {
  if (ms < 1000)
    return `${Math.round(ms)} ms`;
  if (ms < 60000)
    return `${(ms / 1000).toFixed(1)} s`;
  return intervalText(Math.round(ms / 1000));
}

// Every JobState labelled; looked up by the API's state (a string)
const STATE_LABELS: Readonly<Record<string, string>> = { Scheduled: 'Scheduled', Paused: 'Paused', Off: 'Off', OnDemand: 'On demand', Error: 'Error', Blocked: 'Running' } satisfies Record<JobState, string>;

/**
 * The background jobs (sysadmins): what the worker runs on a schedule, when each runs next and how its last runs went,
 * and running one now, pausing it, changing how often it runs or putting it back on its default. Schedules, pauses and
 * runs are in the database, so they hold for every pod and outlive deploys. Read again every few seconds.
 */
@Component({
  selector: 'app-jobs',
  imports: [RouterLink, DatePipe],
  templateUrl: './jobs.component.html',
  styleUrl: './jobs.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobsComponent {
  private api = inject(JobsService);
  private snackBar = inject(MatSnackBar);

  readonly intervalText = intervalText;
  readonly durationText = durationText;
  readonly stateLabels = STATE_LABELS;
  readonly units: IntervalUnit[] = ['seconds', 'minutes', 'hours', 'days'];

  /** Read every few seconds, and again after each action (keeping what's shown meanwhile). */
  readonly overview = rxResource({
    stream: () => this.api.getJobs().pipe(repeat({ delay: JOBS_POLL_MS })),
  });
  readonly jobs = computed<JobDto[]>(() => this.overview.hasValue() ? this.overview.value().jobs : []);
  readonly nodes = computed(() => this.overview.hasValue() ? this.overview.value().nodes : []);

  /** The job whose runs and schedule are shown. */
  readonly selectedKey = signal<JobKey | null>(null);
  readonly selected = computed<JobDto | null>(() => {
    const key = this.selectedKey();
    return key ? this.jobs().find(j => j.group === key.group && j.name === key.name) ?? null : null;
  });
  readonly history = rxResource({
    params: () => this.selectedKey() ?? undefined,
    stream: ({ params }) => this.api.getJobHistory({ group: params.group, name: params.name, take: 50 }).pipe(repeat({ delay: JOBS_POLL_MS })),
  });

  constructor() {
    this.readAgainAfterAnError(this.overview);
    this.readAgainAfterAnError(this.history);
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

  /** The schedule editor's value, for the selected job. */
  readonly intervalValue = signal(1);
  readonly intervalUnit = signal<IntervalUnit>('minutes');
  readonly intervalSeconds = computed(() => Math.round(this.intervalValue() * UNIT_SECONDS[this.intervalUnit()]));

  /** An action waiting for its answer: the buttons wait too. */
  readonly busy = signal(false);

  isSelected(job: JobDto): boolean {
    const key = this.selectedKey();
    return key?.group === job.group && key.name === job.name;
  }

  select(job: JobDto): void {
    this.selectedKey.set({ group: job.group, name: job.name });
    this.editInterval(job.intervalSeconds ?? job.defaultIntervalSeconds ?? 60);
  }

  private editInterval(seconds: number): void {
    const { value, unit } = splitInterval(seconds);
    this.intervalValue.set(value);
    this.intervalUnit.set(unit);
  }

  setIntervalValue(value: string): void {
    this.intervalValue.set(Number(value));
  }

  setIntervalUnit(unit: string): void {
    this.intervalUnit.set(unit as IntervalUnit);
  }

  run(job: JobDto): void {
    this.act(this.api.runJob({ group: job.group, name: job.name }), `${job.displayName} will run in a few seconds.`, "Couldn't run the job.");
  }

  setPaused(job: JobDto, paused: boolean): void {
    this.act(this.api.setJobPaused({ group: job.group, name: job.name, body: { paused } }), paused ? `${job.displayName} is paused.` : `${job.displayName} is resumed.`, "Couldn't change the job.");
  }

  saveInterval(job: JobDto): void {
    const seconds = this.intervalSeconds();
    this.act(this.api.setJobInterval({ group: job.group, name: job.name, body: { intervalSeconds: seconds } }), `${job.displayName} runs every ${intervalText(seconds)}.`, "Couldn't change the schedule.");
  }

  resetSchedule(job: JobDto): void {
    this.act(this.api.resetJobSchedule({ group: job.group, name: job.name }), `${job.displayName} is back on its default schedule.`, "Couldn't reset the schedule.", () => this.editInterval(job.defaultIntervalSeconds ?? 60));
  }

  interrupt(fireInstanceID: string): void {
    this.act(this.api.interruptJob({ fireInstanceID }), 'Asked the run to stop.', "Couldn't stop the run.");
  }

  private act(action: Observable<void>, done: string, failed: string, then?: () => void): void {
    this.busy.set(true);
    action.subscribe({
      next: () => {
        this.busy.set(false);
        this.snackBar.open(done, 'Close', { duration: 4000 });
        this.overview.reload();
        this.history.reload();
        then?.();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }
}
