/** The Telemetry page (sysadmins): this app's own logs, metrics and traces, read back from Loki, Prometheus and Tempo. */
export interface TelemetryStatusDto {
  /** service.name: everything shown is this service's. */
  serviceName: string;
  /** Each false: that backend isn't configured (TelemetryQuery:*Url), its tab says so. */
  logs: boolean;
  metrics: boolean;
  traces: boolean;
}

/** Loki's detected_level values. */
export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';
export const LOG_LEVELS: readonly LogLevel[] = ['trace', 'debug', 'info', 'warn', 'error', 'fatal'];

export interface LogEntryDto {
  timestamp: string;
  level: string;
  message: string;
  traceId: string | null;
  spanId: string | null;
  /** The logger: Serilog's SourceContext. */
  source: string | null;
  pod: string | null;
  /** The line's own properties (RequestPath, StatusCode, exception_*...), the resource's left out. */
  attributes: Record<string, string>;
}

/** Newest first. truncated: the limit was reached, older lines were left out. */
export interface LogsDto {
  entries: LogEntryDto[];
  truncated: boolean;
}

export interface LogsQuery {
  minutes: number;
  levels: LogLevel[];
  search: string;
  traceId: string;
  limit: number;
}

export interface MetricSeriesDto {
  name: string;
  /** [Unix milliseconds, value]. */
  points: [number, number][];
}

export interface MetricPanelDto {
  key: string;
  title: string;
  unit: string;
  series: MetricSeriesDto[];
}

export interface RouteStatsDto {
  route: string;
  requests: number;
  errors: number;
  p95Ms: number | null;
}

export interface MetricsDto {
  start: string;
  end: string;
  stepSeconds: number;
  panels: MetricPanelDto[];
  routes: RouteStatsDto[];
}

export interface TracesQuery {
  minutes: number;
  errorsOnly: boolean;
  minDurationMs: number | null;
}

export interface TraceSummaryDto {
  traceId: string;
  rootName: string;
  rootService: string | null;
  start: string;
  durationMs: number;
  route: string | null;
  statusCode: number | null;
}

export interface SpanEventDto {
  name: string;
  offsetMs: number;
  attributes: Record<string, string>;
}

export interface SpanDto {
  spanId: string;
  parentSpanId: string | null;
  name: string;
  kind: string;
  /** The instrumentation: Microsoft.AspNetCore, System.Net.Http, Microsoft.EntityFrameworkCore... */
  scope: string;
  pod: string | null;
  /** From the trace's first span. */
  offsetMs: number;
  durationMs: number;
  status: 'Unset' | 'Ok' | 'Error';
  statusMessage: string | null;
  attributes: Record<string, string>;
  events: SpanEventDto[];
}

/** This service's spans of a trace, in start order. */
export interface TraceDto {
  traceId: string;
  start: string;
  durationMs: number;
  spans: SpanDto[];
}
