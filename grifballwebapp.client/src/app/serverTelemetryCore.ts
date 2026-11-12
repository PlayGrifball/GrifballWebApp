// The app's server's spans, metrics and log lines (src/server.telemetry.ts sets up the OpenTelemetry SDK and
// hands it the tracer, meter and logger). No Node imports, so it is tested like the rest of the app.
import {
  type Attributes,
  type Context,
  type Logger,
  type Meter,
  type Span,
  type Tracer,
  ROOT_CONTEXT,
  SeverityNumber,
  SpanKind,
  SpanStatusCode,
  context,
  propagation,
  trace,
} from '@opentelemetry/api';
import { redactUrl } from './redactUrl';
import {
  type LogLevel,
  RENDER_DURATION,
  REQUEST_DURATION,
  REQUEST_KIND_ATTRIBUTE,
  finishedStatus,
  formatTraceparent,
  isLogged,
  isTraced,
  logLine,
  requestKind,
  requestLogLevel,
  routeOf,
  serverTimingHeader,
  spanName,
} from './serverTelemetry';

const severity: Record<LogLevel, SeverityNumber> = {
  debug: SeverityNumber.DEBUG,
  info: SeverityNumber.INFO,
  warn: SeverityNumber.WARN,
  error: SeverityNumber.ERROR,
};

/** How a page went: rendered here, sent to render in the browser (app.routes.server.ts), or failed here (and sent to the browser). */
export type RenderOutcome = 'server' | 'browser' | 'failed';

/** What the telemetry reads of a request: Express's, or anything shaped like it. */
export interface ServerRequest {
  method: string;
  path: string;
  originalUrl: string;
  url?: string;
  protocol: string;
  headers: Record<string, string | string[] | undefined>;
}

/** What it uses of a response: Express's, or anything shaped like it. */
export interface ServerResponse {
  statusCode: number;
  writableFinished: boolean;
  setHeader(name: string, value: string): unknown;
  once(event: 'finish' | 'close', listener: () => void): unknown;
}

export interface ServerTelemetryCore {
  /** One log line: stdout, and Loki when exporting. The active span's ids go on it. */
  log(level: LogLevel, message: string, fields?: Record<string, unknown>): void;
  /** A line at info that LOG_LEVEL never hides: what scripts wait for ("Serving"). */
  announce(message: string): void;
  /** Express middleware, first: the request's span, its traceresponse and Server-Timing headers, its metrics and log line. */
  requests(req: ServerRequest, res: ServerResponse, next: () => void): void;
  /** The request's span's context (ROOT_CONTEXT for one not traced, or not seen by `requests`). */
  contextOf(req: object): Context;
  /** Sets traceparent on a request to the backend, from the incoming request's span. */
  inject(req: object, setHeader: (name: string, value: string) => void): void;
  /**
   * Runs a page's render in its own span, under the request's; `render` gets the span's traceparent for the API
   * calls it makes. A request that isn't traced (a build file the static files missed) gets no span, no traceparent
   * and isn't counted. The outcome is taken once `render` is done, so it should cover writing the page too.
   */
  render<T>(req: ServerRequest, render: (traceparent: string | undefined) => Promise<T>, outcome: (result: T) => RenderOutcome): Promise<T>;
}

export interface ServerTelemetryDeps {
  tracer: Tracer;
  meter: Meter;
  /** The OTLP logger; none when nothing is exported. */
  logger?: Logger;
  /** Writes a line to stdout. */
  write: (line: string) => void;
  /** The process's resident memory, in bytes. */
  rss: () => number;
  logLevel: LogLevel;
}

export function createServerTelemetry(deps: ServerTelemetryDeps): ServerTelemetryCore {
  const { tracer, meter, logger: otelLogger } = deps;

  // Named apart from the backend's http.server.request.duration, which shares this service.name: the
  // Telemetry page's request charts would otherwise count each API call twice.
  const requestDuration = meter.createHistogram('grif.frontend.request.duration', REQUEST_DURATION);
  const renderDuration = meter.createHistogram('grif.frontend.render.duration', RENDER_DURATION);
  meter.createObservableGauge('grif.frontend.memory.rss', { unit: 'By', description: 'The server\'s resident memory' })
    .addCallback(result => result.observe(deps.rss()));

  const contexts = new WeakMap<object, Context>();

  function log(level: LogLevel, message: string, fields: Record<string, unknown> = {}): void {
    if (isLogged(level, deps.logLevel))
      write(level, message, fields);
  }

  function announce(message: string): void {
    write('info', message, {});
  }

  function write(level: LogLevel, message: string, fields: Record<string, unknown>): void {
    const span = trace.getActiveSpan()?.spanContext();
    const now = new Date();
    deps.write(logLine(now, level, message, fields, span) + '\n');
    otelLogger?.emit({
      timestamp: now,
      severityNumber: severity[level],
      severityText: level.toUpperCase(),
      body: message,
      attributes: logAttributes(fields),
    });
  }

  function requests(req: ServerRequest, res: ServerResponse, next: () => void): void {
    const kind = requestKind(req.path);
    const start = performance.now();
    const userAgent = header(req, 'user-agent');
    let span: Span | undefined;
    let ctx = ROOT_CONTEXT;
    if (isTraced(kind, userAgent)) {
      // A trace the request came with (the browser's, or a caller's) is continued, as the backend does
      const parent = propagation.extract(ROOT_CONTEXT, req.headers);
      span = tracer.startSpan(spanName(req.method, kind), {
        kind: SpanKind.SERVER,
        attributes: {
          'http.request.method': req.method,
          'http.route': routeOf(kind),
          // What this request is, for the Telemetry page's trace list (TelemetryQueries.TraceRow): page, api, hub, signin
          [REQUEST_KIND_ATTRIBUTE]: kind,
          'url.path': req.path,
          'url.query': redactUrl(req.originalUrl).split('?')[1],
          'url.scheme': req.protocol,
          'user_agent.original': userAgent,
        },
      }, parent);
      ctx = trace.setSpan(parent, span);
      contexts.set(req, ctx);
      const { traceId, spanId, traceFlags } = span.spanContext();
      const traceparent = formatTraceparent(traceId, spanId, (traceFlags & 1) === 1);
      // The backend's own replaces this on what it answers (the same trace, its span)
      res.setHeader('traceresponse', traceparent);
      res.setHeader('Server-Timing', serverTimingHeader(traceparent));
    }

    let done = false;
    const finish = () => {
      if (done)
        return;
      done = true;
      // A response cut short (the visitor left) has no status of its own
      const aborted = !res.writableFinished;
      const status = finishedStatus(res.statusCode, !aborted);
      const seconds = (performance.now() - start) / 1000;
      requestDuration.record(seconds, {
        'http.request.method': req.method,
        'http.route': routeOf(kind),
        'http.response.status_code': status,
      });
      if (!span)
        return;
      if (status !== undefined)
        span.setAttribute('http.response.status_code', status);
      else
        span.setAttribute('grif.aborted', true);
      if ((status ?? 0) >= 500)
        span.setStatus({ code: SpanStatusCode.ERROR });
      context.with(ctx, () => log(requestLogLevel(kind, status ?? 0), aborted ? 'Request aborted' : 'Request finished', {
        method: req.method,
        path: req.path,
        status,
        durationMs: Math.round(seconds * 1000),
        kind,
      }));
      span.end();
    };
    res.once('finish', finish);
    res.once('close', finish);
    context.with(ctx, next);
  }

  function contextOf(req: object): Context {
    return contexts.get(req) ?? ROOT_CONTEXT;
  }

  function inject(req: object, setHeader: (name: string, value: string) => void): void {
    propagation.inject(contextOf(req), undefined, {
      set: (_carrier: undefined, name: string, value: string) => setHeader(name, value),
    });
  }

  async function render<T>(req: ServerRequest, run: (traceparent: string | undefined) => Promise<T>, outcome: (result: T) => RenderOutcome): Promise<T> {
    if (!contexts.has(req))
      return run(undefined);
    const start = performance.now();
    const span = tracer.startSpan('render', { attributes: { 'url.path': redactUrl(req.url).split('?')[0] } }, contextOf(req));
    const { traceId, spanId, traceFlags } = span.spanContext();
    const done = (how: RenderOutcome, error?: unknown) => {
      renderDuration.record((performance.now() - start) / 1000, { outcome: how });
      span.setAttribute('grif.render.outcome', how);
      if (error !== undefined) {
        span.recordException(error instanceof Error ? error : String(error));
        span.setStatus({ code: SpanStatusCode.ERROR });
      }
      span.end();
    };
    try {
      const result = await context.with(trace.setSpan(contextOf(req), span), () => run(formatTraceparent(traceId, spanId, (traceFlags & 1) === 1)));
      done(outcome(result));
      return result;
    } catch (error) {
      done('failed', error);
      throw error;
    }
  }

  return { log, announce, requests, contextOf, inject, render };
}

/** A request header's first value. */
function header(req: ServerRequest, name: string): string | undefined {
  const value = req.headers[name];
  return Array.isArray(value) ? value[0] : value;
}

/** A log line's fields as OpenTelemetry attributes: an error's parts as exception.*, anything else not a primitive as JSON. */
export function logAttributes(fields: Record<string, unknown>): Attributes {
  const attributes: Attributes = {};
  for (const [key, value] of Object.entries(fields)) {
    if (value === undefined || value === null)
      continue;
    if (value instanceof Error) {
      attributes['exception.type'] = value.name;
      attributes['exception.message'] = value.message;
      attributes['exception.stacktrace'] = value.stack;
    } else if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
      attributes[key] = value;
    } else {
      attributes[key] = JSON.stringify(value);
    }
  }
  return attributes;
}
