/** Where a page's run of an on-demand job (a job on the worker, started from the page) is. */
export type OnDemandJobState = 'Idle' | 'Queued' | 'Running';

export interface OnDemandJobStatusDto {
  state: OnDemandJobState;
  /** While running: how far it got (0 to 100) and what it is doing. */
  progress: number | null;
  progressMessage: string | null;
  startedAt: string | null;
  /** The last run that finished (within the week of history kept). */
  lastRun: OnDemandRunDto | null;
}

export interface OnDemandRunDto {
  firedAt: string;
  durationMs: number;
  result: 'Succeeded' | 'Failed' | 'Cancelled' | 'Skipped';
  error: string | null;
}
