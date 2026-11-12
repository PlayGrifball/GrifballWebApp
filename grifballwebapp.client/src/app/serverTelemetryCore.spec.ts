import { AggregationTemporality, MeterProvider, MetricReader } from '@opentelemetry/sdk-metrics';
import { InMemorySpanExporter, SimpleSpanProcessor, TracerProvider } from '@opentelemetry/sdk-trace';
import { announceServing, pageHandler } from './pageHandler';
import { LogLevel } from './serverTelemetry';
import { ServerRequest, ServerResponse, createServerTelemetry } from './serverTelemetryCore';

/** Collects on demand, for a test. */
class TestReader extends MetricReader {
  constructor() {
    super({ aggregationTemporalitySelector: () => AggregationTemporality.CUMULATIVE });
  }
  protected override onShutdown(): Promise<void> { return Promise.resolve(); }
  protected override onForceFlush(): Promise<void> { return Promise.resolve(); }
}

function setUp(logLevel: LogLevel = 'info') {
  const spans = new InMemorySpanExporter();
  const tracerProvider = new TracerProvider({ spanProcessors: [new SimpleSpanProcessor(spans)] });
  const reader = new TestReader();
  const meterProvider = new MeterProvider({ readers: [reader] });
  const lines: string[] = [];
  const telemetry = createServerTelemetry({
    tracer: tracerProvider.getTracer('test'),
    meter: meterProvider.getMeter('test'),
    write: line => lines.push(line),
    rss: () => 1,
    logLevel,
  });
  /** How many renders the render-duration histogram has counted. */
  const renders = async () => {
    const { resourceMetrics } = await reader.collect();
    const metric = resourceMetrics.scopeMetrics.flatMap(s => s.metrics).find(m => m.descriptor.name === 'grif.frontend.render.duration');
    return (metric?.dataPoints ?? []).reduce((n, p) => n + (p.value as { count: number }).count, 0);
  };
  return { telemetry, spans, lines, renders };
}

function request(path: string): ServerRequest {
  return { method: 'GET', path, originalUrl: path, url: path, protocol: 'https', headers: { 'user-agent': 'Mozilla/5.0' } };
}

function response(statusCode = 200): ServerResponse & { finish(): void } {
  const listeners: (() => void)[] = [];
  return {
    statusCode,
    writableFinished: true,
    setHeader: () => undefined,
    once: (_event, listener) => listeners.push(listener),
    finish: () => listeners.forEach(l => l()),
  };
}

describe('server telemetry core', () => {
  it('makes no render span, and counts no render, for a request it does not trace (an old build file)', async () => {
    const { telemetry, spans, renders } = setUp();
    const req = request('/chunk-OLDHASH1.js');
    const res = response(404);
    telemetry.requests(req, res, () => undefined);

    await telemetry.render(req, async () => null, () => 'browser');
    res.finish();

    expect(spans.getFinishedSpans().map(s => s.name)).toEqual([]);
    expect(await renders()).toBe(0);
  });

  it('renders a page in a span under the request\'s', async () => {
    const { telemetry, spans, renders } = setUp();
    const req = request('/season/12');
    const res = response();
    telemetry.requests(req, res, () => undefined);

    await telemetry.render(req, async () => 'page', () => 'server');
    res.finish();

    const [render, server] = spans.getFinishedSpans();
    expect(render.name).toBe('render');
    expect(server.name).toBe('GET /*');
    expect(render.parentSpanContext?.spanId).toBe(server.spanContext().spanId);
    expect(await renders()).toBe(1);
  });

  it('prints the line the scripts wait for ("Serving") whatever LOG_LEVEL says', () => {
    const { telemetry, lines } = setUp('error');

    announceServing(telemetry, 80, 'http://backend');

    expect(lines.join('')).toContain('Serving on http://localhost:80, the API at http://backend');
  });

  it('counts a page that fails after rendering (writing it) as failed, not rendered', async () => {
    const { telemetry, spans } = setUp();
    const req = request('/season/12') as ServerRequest & { headers: { cookie?: string; traceparent?: string } };
    let sentClientPage!: () => void;
    const sent = new Promise<void>(resolve => sentClientPage = resolve);

    pageHandler({
      handle: async () => new Response('<html><body></body></html>', { headers: { 'Content-Type': 'text/html' } }),
      write: () => { throw new Error('socket closed'); },
      sendClientPage: () => sentClientPage(),
      telemetry,
    })(req, {}, () => undefined);
    await sent;

    const render = spans.getFinishedSpans().find(s => s.name === 'render');
    expect(render?.attributes['grif.render.outcome']).toBe('failed');
  });
});
