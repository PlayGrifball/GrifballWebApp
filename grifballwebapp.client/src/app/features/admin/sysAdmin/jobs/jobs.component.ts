import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { JobDto } from '@api/models';
import { JobKey, JobState } from '../../api/jobs';
import { intervalText, JobsStore } from './jobs.store';
import { SkeletonComponent } from '@shared/skeleton/skeleton.component';

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

/** A job's key in the drafts. */
function draftKey(job: JobDto): string {
  return `${job.group}/${job.name}`;
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
  imports: [RouterLink, DatePipe, SkeletonComponent],
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
  /** The jobs table's headed columns; the actions column follows them. */
  readonly headings = ['Job', 'Every', 'State', 'Next run', 'Last run', 'Runs'] as const;
  /** How many columns the table has, so the details and empty rows span them all as the columns change. */
  readonly columns = this.headings.length + 1;

  readonly jobs = this.store.jobs;
  readonly nodes = this.store.nodes;
  readonly loaded = this.store.loaded;
  readonly unreachable = this.store.unreachable;
  readonly firstLoad = this.store.firstLoad;

  /** The job whose runs and schedule are shown. */
  readonly selectedKey = signal<JobKey | null>(null);
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

  /** Each job's unsaved interval, kept while another job's details are open, so coming back finds the edit as it was left. */
  private readonly drafts = new Map<string, number>();

  /** Opens a job's details under its row; its Details button again closes them. An unsaved interval edit is kept for when it opens again. */
  select(job: JobDto): void {
    this.keepDraft();
    if (this.isSelected(job)) {
      this.selectedKey.set(null);
      return;
    }
    this.selectedKey.set({ group: job.group, name: job.name });
    this.editInterval(this.drafts.get(draftKey(job)) ?? job.intervalSeconds ?? job.defaultIntervalSeconds ?? 60);
  }

  /** The open job's interval edit, kept when it differs from its schedule (and dropped when it doesn't). */
  private keepDraft(): void {
    const job = this.jobs().find(j => this.isSelected(j));
    if (!job)
      return;
    const key = draftKey(job);
    if (this.intervalSeconds() === (job.intervalSeconds ?? job.defaultIntervalSeconds ?? 60))
      this.drafts.delete(key);
    else
      this.drafts.set(key, this.intervalSeconds());
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
    this.drafts.delete(draftKey(job));
    this.store.setInterval(job, this.intervalSeconds());
  }

  resetSchedule(job: JobDto): void {
    this.drafts.delete(draftKey(job));
    this.store.resetSchedule(job, () => this.editInterval(job.defaultIntervalSeconds ?? 60));
  }

  interrupt(fireInstanceID: string): void {
    this.store.interrupt(fireInstanceID);
  }
}
