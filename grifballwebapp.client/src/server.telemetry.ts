// The app's server's OpenTelemetry: a span per request (and one for each page it renders), its log lines
// (JSON on stdout, and to Loki over OTLP with their trace ids, as the backend's) and its metrics, pushed over
// OTLP/HTTP when OTEL_EXPORTER_OTLP_ENDPOINT is set (app/serverTelemetry.ts has the settings). Without it,
// spans are still made, so the backend still continues the trace and the logs still carry its ids.
//
// This sets up the SDK; what the server does with it is app/serverTelemetryCore.ts. Node only, and never
// imported by the app: the browser's bundle (and its tests) stay without it.
import { context, logs, metrics, propagation, trace } from '@opentelemetry/api';
import { AsyncLocalStorageContextManager } from '@opentelemetry/context-async-hooks';
import { W3CTraceContextPropagator } from '@opentelemetry/core';
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-http';
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-http';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { BatchLogRecordProcessor, LoggerProvider } from '@opentelemetry/sdk-logs';
import { MeterProvider, PeriodicExportingMetricReader } from '@opentelemetry/sdk-metrics';
import { BatchSpanProcessor, TracerProvider } from '@opentelemetry/sdk-trace';
import type { ServerTelemetrySettings } from './app/core/server/serverTelemetry';
import { type ServerTelemetryCore, createServerTelemetry } from './app/core/server/serverTelemetryCore';

const SCOPE = 'grifballwebapp.client';

export interface ServerTelemetry extends ServerTelemetryCore {
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

  const core = createServerTelemetry({
    tracer: trace.getTracer(SCOPE),
    meter: meterProvider.getMeter(SCOPE),
    logger: loggerProvider?.getLogger(SCOPE),
    write: line => process.stdout.write(line),
    rss: () => process.memoryUsage.rss(),
    logLevel: settings.logLevel,
    browserTelemetry: settings.browserTelemetry,
  });

  async function shutdown(): Promise<void> {
    await Promise.allSettled([tracerProvider.shutdown(), meterProvider.shutdown(), loggerProvider?.shutdown()]);
  }

  return { ...core, shutdown };
}
