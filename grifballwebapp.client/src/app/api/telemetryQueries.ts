/** The Telemetry page's filters: what it asks the API for. */

/** Loki's detected_level values. */
export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';
export const LOG_LEVELS: readonly LogLevel[] = ['trace', 'debug', 'info', 'warn', 'error', 'fatal'];

export interface LogsQuery {
  minutes: number;
  levels: LogLevel[];
  search: string;
  traceId: string;
  limit: number;
}

export interface TracesQuery {
  minutes: number;
  errorsOnly: boolean;
  minDurationMs: number | null;
}
