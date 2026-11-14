import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { JobDto } from '@api/models';
import { JobKey, JobState } from '../../api/jobs';
import { intervalText, JobsStore } from './jobs.store';

export type IntervalUnit = 'seconds' | 'minutes' | 'hours' | 'days';

export const UNIT_SECONDS: Record<IntervalUnit, number> = { seconds: 1, minutes: 60, hours: 3600, days: 86400 };

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
const STATE_LABELS: Readonly<Record<string, string>> = { Scheduled: 'Scheduled', Paused: 'Paused', Off: 'Off', Manual: 'Run by hand', OnDemand: 'On demand', Error: 'Error', Blocked: 'Running' } satisfies Record<JobState, string>;

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
  providers: [JobsStore],
})
export class JobsComponent {
  private readonly store = inject(JobsStore);

  readonly intervalText = intervalText;
  readonly durationText = durationText;
  readonly stateLabels = STATE_LABELS;
  readonly units: IntervalUnit[] = ['seconds', 'minutes', 'hours', 'days'];

  readonly jobs = this.store.jobs;
  readonly nodes = this.store.nodes;
  readonly loaded = this.store.loaded;
  readonly failed = this.store.failed;

  /** The job whose runs and schedule are shown. */
  readonly selectedKey = signal<JobKey | null>(null);
  readonly selected = computed<JobDto | null>(() => {
    const key = this.selectedKey();
    return key ? this.jobs().find(j => j.group === key.group && j.name === key.name) ?? null : null;
  });
  /** The chosen job's last runs; null until they are read. */
  readonly runs = this.store.runs;
  readonly runsFailed = this.store.runsFailed;

  constructor() {
    this.store.selectedKey.use(this.selectedKey);
  }

  /** The schedule editor's value, for the selected job. */
  readonly intervalValue = signal(1);
  readonly intervalUnit = signal<IntervalUnit>('minutes');
  readonly intervalSeconds = computed(() => Math.round(this.intervalValue() * UNIT_SECONDS[this.intervalUnit()]));

  /** An action waiting for its answer: the buttons wait too. */
  readonly busy = this.store.busy;

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
    this.store.run(job);
  }

  setPaused(job: JobDto, paused: boolean): void {
    this.store.setPaused(job, paused);
  }

  saveInterval(job: JobDto): void {
    this.store.setInterval(job, this.intervalSeconds());
  }

  resetSchedule(job: JobDto): void {
    this.store.resetSchedule(job, () => this.editInterval(job.defaultIntervalSeconds ?? 60));
  }

  interrupt(fireInstanceID: string): void {
    this.store.interrupt(fireInstanceID);
  }
}
