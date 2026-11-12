// The app's server's telemetry (src/server.telemetry.ts), the parts that need no Node: its settings, the
// requests it traces, and its log lines. Kept here, apart from the OpenTelemetry SDK, so they are tested
// like the rest of the app.

/** Log levels, lowest first, as Loki's detected_level names them. */
export const LOG_LEVELS = ['debug', 'info', 'warn', 'error'] as const;
export type LogLevel = typeof LOG_LEVELS[number];

/**
 * The server's telemetry settings, from the standard OpenTelemetry environment variables, as the backend
 * reads them (GrifballWebApp.Server/Telemetry/TelemetryOptions.cs):
 *
 *   OTEL_EXPORTER_OTLP_ENDPOINT  the collector's OTLP/HTTP base (ex. http://otel-collector:4318). Unset:
 *                                nothing is exported, and logs only go to stdout.
 *   OTEL_SERVICE_NAME            service.name. The chart sets the backend's, so the Telemetry page (and
 *                                telemetry-gate, which allows one service.name per environment) sees both.
 *   OTEL_RESOURCE_ATTRIBUTES     more resource attributes, key=value,key=value
 *   OTEL_METRIC_EXPORT_INTERVAL  milliseconds between metric pushes, 5 s - 5 min (default 30 s)
 *   HOSTNAME                     service.instance.id: the pod's name
 *   LOG_LEVEL                    the lowest level logged: debug, info (default), warn or error
 */
export interface ServerTelemetrySettings {
  /** The collector's OTLP/HTTP base, without a trailing slash; null when export is off. */
  endpoint: string | null;
  serviceName: string;
  /** Every resource attribute, service.* included. */
  resource: Record<string, string>;
  metricIntervalMs: number;
  logLevel: LogLevel;
}

export const DEFAULT_SERVICE_NAME = 'grifballwebapp-client';
/** Which part of the app sent it: the backend's telemetry shares this service.name. */
export const COMPONENT_ATTRIBUTE = 'service.component';
export const COMPONENT = 'frontend';
export const DEFAULT_METRIC_INTERVAL_MS = 30_000;
export const MIN_METRIC_INTERVAL_MS = 5_000;
export const MAX_METRIC_INTERVAL_MS = 300_000;

export function serverTelemetrySettings(env: Record<string, string | undefined>): ServerTelemetrySettings {
  const resource = parseResourceAttributes(env['OTEL_RESOURCE_ATTRIBUTES']);
  const serviceName = env['OTEL_SERVICE_NAME']?.trim() || DEFAULT_SERVICE_NAME;
  resource['service.name'] = serviceName;
  resource[COMPONENT_ATTRIBUTE] = COMPONENT;
  const instance = env['HOSTNAME']?.trim();
  if (instance)
    resource['service.instance.id'] = instance;
  return {
    endpoint: otlpEndpoint(env['OTEL_EXPORTER_OTLP_ENDPOINT']),
    serviceName,
    resource,
    metricIntervalMs: metricInterval(env['OTEL_METRIC_EXPORT_INTERVAL']),
    logLevel: logLevel(env['LOG_LEVEL']),
  };
}

/** key=value,key=value; values URL-decoded, empty ones and pairs without a key left out. */
export function parseResourceAttributes(value: string | undefined): Record<string, string> {
  const result: Record<string, string> = {};
  for (const pair of (value ?? '').split(',')) {
    const separator = pair.indexOf('=');
    if (separator <= 0)
      continue;
    const key = pair.slice(0, separator).trim();
    let attribute = pair.slice(separator + 1).trim();
    try {
      attribute = decodeURIComponent(attribute);
    } catch {
      // Not URL-encoded after all: kept as it is
    }
    if (key && attribute)
      result[key] = attribute;
  }
  return result;
}

/** An http(s) URL without its trailing slashes, else null (export off). */
export function otlpEndpoint(value: string | undefined): string | null {
  const text = value?.trim();
  if (!text || !URL.canParse(text))
    return null;
  const url = new URL(text);
  if (url.protocol !== 'http:' && url.protocol !== 'https:')
    return null;
  let end = text.length;
  while (end > 0 && text[end - 1] === '/')
    end--;
  return text.slice(0, end);
}

export function metricInterval(value: string | undefined): number {
  const ms = Number(value);
  if (!value?.trim() || !Number.isFinite(ms))
    return DEFAULT_METRIC_INTERVAL_MS;
  return Math.min(MAX_METRIC_INTERVAL_MS, Math.max(MIN_METRIC_INTERVAL_MS, ms));
}

export function logLevel(value: string | undefined): LogLevel {
  const level = value?.trim().toLowerCase();
  return LOG_LEVELS.find(l => l === level) ?? 'info';
}

/** Whether a line at `level` is logged when the lowest is `minimum`. */
export function isLogged(level: LogLevel, minimum: LogLevel): boolean {
  return LOG_LEVELS.indexOf(level) >= LOG_LEVELS.indexOf(minimum);
}

/**
 * What a request to the server is, which names its span and its metrics' http.route: the API, the hubs and
 * the Discord callback (passed on to the backend), the build's files, or a page.
 */
export type RequestKind = 'api' | 'hub' | 'signin' | 'file' | 'page';

export function requestKind(path: string): RequestKind {
  if (path === '/api' || path.startsWith('/api/'))
    return 'api';
  if (path === '/hub' || path.startsWith('/hub/'))
    return 'hub';
  if (path === '/signin-discord' || path.startsWith('/signin-discord/'))
    return 'signin';
  // A file name's extension in the last segment: main-<hash>.js, media/x.woff2, favicon.ico
  return /\.[A-Za-z0-9]{1,8}$/.test(path) ? 'file' : 'page';
}

/** Kubernetes' probes: every few seconds on every pod, never traced or logged. */
export function isProbe(userAgent: string | undefined): boolean {
  return userAgent?.startsWith('kube-probe/') ?? false;
}

/**
 * Whether a request gets a span and a log line: not the build's files (each page loads dozens, and the CDN
 * keeps them) and not probes. They are still counted in the metrics.
 */
export function isTraced(kind: RequestKind, userAgent: string | undefined): boolean {
  return kind !== 'file' && !isProbe(userAgent);
}

/** A span's name, as the OpenTelemetry HTTP conventions want it: the method and a low-cardinality route. */
export function spanName(method: string, kind: RequestKind): string {
  return `${method} ${routeOf(kind)}`;
}

/** http.route for each kind: what the backend sees is /api/<route>; the server only knows the prefix. */
export function routeOf(kind: RequestKind): string {
  switch (kind) {
    case 'api': return '/api/*';
    case 'hub': return '/hub/*';
    case 'signin': return '/signin-discord';
    case 'file': return '/*.*';
    case 'page': return '/*';
  }
}

/** The level a finished request is logged at: errors for 5xx, debug for what the backend logs itself. */
export function requestLogLevel(kind: RequestKind, status: number): LogLevel {
  if (status >= 500)
    return 'error';
  return kind === 'page' ? 'info' : 'debug';
}

/** `00-{trace-id}-{span-id}-{flags}` (W3C Trace Context), as traceparent and traceresponse carry it. */
export function formatTraceparent(traceId: string, spanId: string, sampled: boolean): string {
  return `00-${traceId}-${spanId}-${sampled ? '01' : '00'}`;
}

/**
 * The Server-Timing header that hands a page its trace: scripts can't read a page's own response headers,
 * but they can read this one (performance.getEntriesByType('navigation')[0].serverTiming), and the browser's
 * error reports (app/clientErrors.ts) send it back.
 */
export function serverTimingHeader(traceparent: string): string {
  return `traceparent;desc="${traceparent}"`;
}

/** The trace ids a log line carries, as the backend's do (trace_id, span_id). */
export interface LogTrace {
  traceId: string;
  spanId: string;
}

/**
 * One JSON log line for stdout: time, level and message first, then the fields, then the trace. A field
 * named like one of those is dropped rather than overwrite it.
 */
export function logLine(time: Date, level: LogLevel, message: string, fields: Record<string, unknown> = {}, trace?: LogTrace): string {
  const line: Record<string, unknown> = { time: time.toISOString(), level, msg: message };
  for (const [key, value] of Object.entries(fields)) {
    if (!(key in line) && key !== 'trace_id' && key !== 'span_id' && value !== undefined)
      line[key] = value instanceof Error ? errorFields(value) : value;
  }
  if (trace) {
    line['trace_id'] = trace.traceId;
    line['span_id'] = trace.spanId;
  }
  return JSON.stringify(line);
}

/** An Error as a log field: JSON.stringify would give {}. */
export function errorFields(error: Error): { type: string; message: string; stack?: string } {
  return { type: error.name, message: error.message, stack: error.stack };
}
