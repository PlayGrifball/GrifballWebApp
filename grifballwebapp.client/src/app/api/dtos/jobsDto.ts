/** The Jobs page (sysadmins): the background jobs Quartz runs on the worker. */
export interface JobsOverviewDto {
  jobs: JobDto[];
  /** The schedulers checked in to the store (clustered: SQL Server and Postgres); empty on SQLite. */
  nodes: JobNodeDto[];
}

export type JobState = 'Scheduled' | 'Paused' | 'Off' | 'Error' | 'Blocked';

export interface JobDto {
  group: string;
  name: string;
  description: string | null;
  /** One of the app's recurring jobs: its schedule can be changed and reset. */
  recurring: boolean;
  /** Off (a Discord job while Discord is off): no trigger, and it can't be run. */
  enabled: boolean;
  state: JobState;
  intervalSeconds: number | null;
  defaultIntervalSeconds: number | null;
  nextRunAt: string | null;
  previousRunAt: string | null;
  pausedReason: string | null;
  pausedBy: string | null;
  pausedAt: string | null;
  running: JobRunningDto[];
  status: JobStatusDto | null;
}

export interface JobRunningDto {
  fireInstanceID: string;
  startedAt: string;
  node: string;
}

export interface JobStatusDto {
  lastResult: string;
  lastRunAt: string;
  lastDurationMs: number;
  lastSucceededAt: string | null;
  lastFailedAt: string | null;
  lastFailureMessage: string | null;
  consecutiveFailures: number;
  runCount: number;
  failureCount: number;
}

export interface JobRunDto {
  firedAt: string;
  durationMs: number;
  result: string;
  error: string | null;
  node: string;
  /** Run from the Jobs page rather than by its schedule. */
  manual: boolean;
  trigger: string;
}

export interface JobNodeDto {
  instanceID: string;
  lastCheckInAt: string | null;
  state: string;
}

export interface JobKeyDto {
  group: string;
  name: string;
}
