// The app's server's OpenTelemetry: a span per request (and one for each page it renders), its log lines
// (JSON on stdout, and to Loki over OTLP with their trace ids, as the backend's) and its metrics, pushed over
// OTLP/HTTP when OTEL_EXPORTER_OTLP_ENDPOINT is set (app/serverTelemetry.ts has the settings). Without it,
// spans are still made, so the backend still continues the trace and the logs still carry its ids.
//
// Node only, and never imported by the app: the browser's bundle (and its tests) stay without it.
import {
  type Attributes,
  type Context,
  type Span,
  ROOT_CONTEXT,
  SeverityNumber,
  SpanKind,
  SpanStatusCode,
  context,
  logs,
  metrics,
  propagation,
  trace,
} from '@opentelemetry/api';
import { AsyncLocalStorageContextManager } from '@opentelemetry/context-async-hooks';
import { W3CTraceContextPropagator } from '@opentelemetry/core';
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-http';
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-http';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { BatchLogRecordProcessor, LoggerProvider } from '@opentelemetry/sdk-logs';
import { MeterProvider, PeriodicExportingMetricReader } from '@opentelemetry/sdk-metrics';
import { BatchSpanProcessor, TracerProvider } from '@opentelemetry/sdk-trace';
import type { NextFunction, Request, Response } from 'express';
import type { IncomingMessage } from 'node:http';
import {
  type LogLevel,
  RENDER_DURATION,
  REQUEST_DURATION,
  type ServerTelemetrySettings,
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
} from './app/serverTelemetry';
import { redactUrl } from './app/redactUrl';

const SCOPE = 'grifballwebapp.client';

const severity: Record<LogLevel, SeverityNumber> = {
  debug: SeverityNumber.DEBUG,
  info: SeverityNumber.INFO,
  warn: SeverityNumber.WARN,
  error: SeverityNumber.ERROR,
};

/** How a page went: rendered here, sent to render in the browser (app.routes.server.ts), or failed here (and sent to the browser). */
export type RenderOutcome = 'server' | 'browser' | 'failed';

export interface ServerTelemetry {
  /** One log line: stdout, and Loki when exporting. The active span's ids go on it. */
  log(level: LogLevel, message: string, fields?: Record<string, unknown>): void;
  /** Express middleware, first: the request's span, its traceresponse and Server-Timing headers, its metrics and log line. */
  requests(req: Request, res: Response, next: NextFunction): void;
  /** The request's span's context (ROOT_CONTEXT for one not traced, or not seen by `requests`). */
  contextOf(req: IncomingMessage): Context;
  /** Sets traceparent on a request to the backend, from the incoming request's span. */
  inject(req: IncomingMessage, setHeader: (name: string, value: string) => void): void;
  /** Runs a page's render in its own span, under the request's; `render` gets the span's traceparent for the API calls it makes. */
  render<T>(req: IncomingMessage, render: (traceparent: string) => Promise<T>, outcome: (result: T) => RenderOutcome): Promise<T>;
  /** Sends what is queued; called on SIGTERM. */
  shutdown(): Promise<void>;
}

export function startServerTelemetry(settings: ServerTelemetrySettings): ServerTelemetry {
  const resource = resourceFromAttributes(settings.resource);
  const endpoint = settings.endpoint;

  const tracerProvider = new TracerProvider({
    resource,
    spanProcessors: endpoint ? [new BatchSpanProcessor({ exporter: new OTLPTraceExporter({ url: `${endpoint}/v1/traces` }) })] : [],
  });
  const loggerProvider = endpoint
    ? new LoggerProvider({ resource, processors: [new BatchLogRecordProcessor({ exporter: new OTLPLogExporter({ url: `${endpoint}/v1/logs` }) })] })
    : null;
  const meterProvider = new MeterProvider({
    resource,
    readers: endpoint
      ? [new PeriodicExportingMetricReader({ exporter: new OTLPMetricExporter({ url: `${endpoint}/v1/metrics` }), exportIntervalMillis: settings.metricIntervalMs })]
      : [],
  });

  context.setGlobalContextManager(new AsyncLocalStorageContextManager().enable());
  propagation.setGlobalPropagator(new W3CTraceContextPropagator());
  trace.setGlobalTracerProvider(tracerProvider);
  metrics.setGlobalMeterProvider(meterProvider);
  if (loggerProvider)
    logs.setGlobalLoggerProvider(loggerProvider);

  const tracer = trace.getTracer(SCOPE);
  const otelLogger = loggerProvider?.getLogger(SCOPE);
  const meter = meterProvider.getMeter(SCOPE);

  // Named apart from the backend's http.server.request.duration, which shares this service.name: the
  // Telemetry page's request charts would otherwise count each API call twice.
  const requestDuration = meter.createHistogram('grif.frontend.request.duration', REQUEST_DURATION);
  const renderDuration = meter.createHistogram('grif.frontend.render.duration', RENDER_DURATION);
  meter.createObservableGauge('grif.frontend.memory.rss', { unit: 'By', description: 'The server\'s resident memory' })
    .addCallback(result => result.observe(process.memoryUsage.rss()));

  const contexts = new WeakMap<IncomingMessage, Context>();

  function log(level: LogLevel, message: string, fields: Record<string, unknown> = {}): void {
    if (!isLogged(level, settings.logLevel))
      return;
    const span = trace.getActiveSpan()?.spanContext();
    const now = new Date();
    process.stdout.write(logLine(now, level, message, fields, span) + '\n');
    otelLogger?.emit({
      timestamp: now,
      severityNumber: severity[level],
      severityText: level.toUpperCase(),
      body: message,
      attributes: logAttributes(fields),
    });
  }

  function requests(req: Request, res: Response, next: NextFunction): void {
    const kind = requestKind(req.path);
    const start = performance.now();
    const userAgent = req.headers['user-agent'];
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

  function contextOf(req: IncomingMessage): Context {
    return contexts.get(req) ?? ROOT_CONTEXT;
  }

  function inject(req: IncomingMessage, setHeader: (name: string, value: string) => void): void {
    propagation.inject(contextOf(req), undefined, {
      set: (_carrier: undefined, name: string, value: string) => setHeader(name, value),
    });
  }

  async function render<T>(req: IncomingMessage, run: (traceparent: string) => Promise<T>, outcome: (result: T) => RenderOutcome): Promise<T> {
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

  async function shutdown(): Promise<void> {
    await Promise.allSettled([tracerProvider.shutdown(), meterProvider.shutdown(), loggerProvider?.shutdown()]);
  }

  return { log, requests, contextOf, inject, render, shutdown };
}

/** A log line's fields as OpenTelemetry attributes: an error's parts as exception.*, anything else not a primitive as JSON. */
function logAttributes(fields: Record<string, unknown>): Attributes {
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
