import { propagation } from '@opentelemetry/api';
import { W3CTraceContextPropagator } from '@opentelemetry/core';
import { AggregationTemporality, MeterProvider, MetricReader } from '@opentelemetry/sdk-metrics';
import { InMemorySpanExporter, SimpleSpanProcessor, TracerProvider } from '@opentelemetry/sdk-trace';
import { announceServing, pageHandler } from './pageHandler';
import { LogLevel } from './serverTelemetry';
import { ServerRequest, ServerResponse, createServerTelemetry, logAttributes } from './serverTelemetryCore';

/** Collects on demand, for a test. */
class TestReader extends MetricReader {
  constructor() {
    super({ aggregationTemporalitySelector: () => AggregationTemporality.CUMULATIVE });
  }
  protected override onShutdown(): Promise<void> { return Promise.resolve(); }
  protected override onForceFlush(): Promise<void> { return Promise.resolve(); }
}

function setUp(logLevel: LogLevel = 'info', emit?: jasmine.Spy, extra: Record<string, unknown> = {}) {
  const spans = new InMemorySpanExporter();
  const tracerProvider = new TracerProvider({ spanProcessors: [new SimpleSpanProcessor({ exporter: spans })] });
  const reader = new TestReader();
  const meterProvider = new MeterProvider({ readers: [reader] });
  const lines: string[] = [];
  const telemetry = createServerTelemetry({
    tracer: tracerProvider.getTracer('test'),
    meter: meterProvider.getMeter('test'),
    write: line => lines.push(line),
    rss: () => 1,
    logLevel,
    logger: emit ? ({ emit } as never) : undefined,
    ...extra,
  } as Parameters<typeof createServerTelemetry>[0]);
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

function response(statusCode = 200, writableFinished = true): ServerResponse & { finish(): void; headers: Record<string, string> } {
  const listeners: (() => void)[] = [];
  const headers: Record<string, string> = {};
  return {
    statusCode,
    writableFinished,
    headers,
    setHeader: (name, value) => headers[name] = value,
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
    // As server.ts has it: the request middleware first, which traces the page
    telemetry.requests(req, response(), () => undefined);
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

  it('logs a line at or above LOG_LEVEL to stdout and the OTLP logger, its fields as attributes', () => {
    const emit = jasmine.createSpy('emit');
    const { telemetry, lines } = setUp('warn', emit);

    telemetry.log('info', 'hidden');
    telemetry.log('error', 'shown', { path: '/x', error: new TypeError('boom'), detail: { a: 1 }, none: null });

    expect(lines.length).toBe(1);
    expect(JSON.parse(lines[0]).msg).toBe('shown');
    expect(emit).toHaveBeenCalledOnceWith(jasmine.objectContaining({ body: 'shown', severityText: 'ERROR' }));
  });

  it('turns a log line\'s fields into attributes: errors as exception.*, objects as JSON, nothing for null', () => {
    const error = new TypeError('boom');

    expect(logAttributes({ path: '/x', count: 2, ok: true, error, detail: { a: 1 }, none: null, gone: undefined })).toEqual({
      path: '/x', count: 2, ok: true, 'exception.type': 'TypeError', 'exception.message': 'boom', 'exception.stacktrace': error.stack, detail: '{"a":1}',
    });
  });

  it('records no status, but aborted, for a response the visitor cut short, and an error for a 5xx', () => {
    const { telemetry, spans } = setUp();
    const cut = response(200, false);
    const failed = response(502);
    telemetry.requests(request('/season/1'), cut, () => undefined);
    telemetry.requests({ ...request('/api/Teams'), headers: { 'user-agent': ['Mozilla/5.0', 'other'] } }, failed, () => undefined);

    cut.finish();
    failed.finish();

    const [aborted, error] = spans.getFinishedSpans();
    expect(aborted.attributes['grif.aborted']).toBeTrue();
    expect(aborted.attributes['http.response.status_code']).toBeUndefined();
    expect(error.status.code).toBe(2);
    expect(error.attributes['user_agent.original']).toBe('Mozilla/5.0');
  });

  it('passes a traced request\'s trace on to the backend, and nothing for one it never saw', () => {
    propagation.setGlobalPropagator(new W3CTraceContextPropagator());
    try {
      const { telemetry } = setUp();
      const req = request('/api/Teams');
      const res = response();
      telemetry.requests(req, res, () => undefined);
      const sent: Record<string, string> = {};
      const unseen: Record<string, string> = {};

      telemetry.inject(req, (name, value) => sent[name] = value);
      telemetry.inject(request('/api/Other'), (name, value) => unseen[name] = value);

      expect(sent['traceparent']).toBe(res.headers['traceresponse']);
      expect(unseen).toEqual({});
    } finally {
      propagation.disable();
    }
  });

  it('records a render that throws as failed, with what it threw', async () => {
    const { telemetry, spans } = setUp();
    const req = request('/season/12');
    telemetry.requests(req, response(), () => undefined);

    await expectAsync(telemetry.render(req, () => Promise.reject('not an Error'), () => 'server')).toBeRejectedWith('not an Error');

    const render = spans.getFinishedSpans().find(s => s.name === 'render')!;
    expect(render.attributes['grif.render.outcome']).toBe('failed');
    expect(render.events[0].attributes?.['exception.message']).toBe('not an Error');
  });

  describe('the page handler', () => {
    function run(handle: () => Promise<Response | null>) {
      const { telemetry, spans } = setUp();
      const req = request('/season/12') as ServerRequest & { headers: { cookie?: string; traceparent?: string } };
      telemetry.requests(req, response(), () => undefined);
      const written = jasmine.createSpy('write');
      const sentClientPage = jasmine.createSpy('sendClientPage');
      pageHandler({ handle, write: written, sendClientPage: sentClientPage, telemetry })(req, {}, () => undefined);
      const outcome = async () => {
        await new Promise(resolve => setTimeout(resolve));
        return spans.getFinishedSpans().find(s => s.name === 'render')?.attributes['grif.render.outcome'];
      };
      return { req, written, sentClientPage, outcome };
    }

    it('sends the page that renders in the browser for a route that does, as outcome browser', async () => {
      const { written, sentClientPage, outcome } = run(async () => null);

      expect(await outcome()).toBe('browser');
      expect(sentClientPage).toHaveBeenCalled();
      expect(written).not.toHaveBeenCalled();
    });

    it('writes a rendered page with its trace on its API calls, as outcome server', async () => {
      const { req, written, sentClientPage, outcome } = run(async () => new Response('{}', { headers: { 'Content-Type': 'application/json' } }));

      expect(await outcome()).toBe('server');
      expect(written).toHaveBeenCalled();
      expect(sentClientPage).not.toHaveBeenCalled();
      expect(req.headers.traceparent).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/);
    });
  });

  it('traces none of the browser\'s telemetry batches (every few seconds a tab), as the backend doesn\'t', () => {
    const { telemetry, spans } = setUp();
    const res = response(204);
    telemetry.requests({ ...request('/api/client-telemetry'), method: 'POST' }, res, () => undefined);
    res.finish();

    expect(spans.getFinishedSpans()).toEqual([]);
  });

  it('tells the page browser telemetry is on in its Server-Timing, only when it is', () => {
    const on = response();
    const off = response();
    setUp('info', undefined, { browserTelemetry: true }).telemetry.requests(request('/season/1'), on, () => undefined);
    setUp().telemetry.requests(request('/season/1'), off, () => undefined);

    expect(on.headers['Server-Timing']).toContain('browser-telemetry');
    expect(off.headers['Server-Timing']).not.toContain('browser-telemetry');
  });
});
