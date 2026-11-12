import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, type TestRequest } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { msText, TelemetryComponent, waterfall } from './telemetry.component';
import { formatValue, niceMax } from './lineChart.component';
import type { LogEntryDto, MetricsDto, SpanDto, TelemetryStatusDto, TraceDto, TraceSummaryDto } from '../../api/dtos/telemetryDto';

const allOn: TelemetryStatusDto = { serviceName: 'grif-prod', logs: true, metrics: true, traces: true };

function span(spanId: string, changes: Partial<SpanDto> = {}): SpanDto {
  return {
    spanId, parentSpanId: null, name: spanId, kind: 'Internal', scope: 'test', pod: 'grif-backend-1', offsetMs: 0, durationMs: 10,
    status: 'Unset', statusMessage: null, attributes: {}, events: [], ...changes,
  };
}

const trace = (spans: SpanDto[], durationMs = 100): TraceDto => ({ traceId: 'abc', start: '2026-11-03T12:00:00Z', durationMs, spans });

const metrics: MetricsDto = {
  start: '2026-11-03T11:00:00Z', end: '2026-11-03T12:00:00Z', stepSeconds: 24,
  panels: [
    { key: 'requests', title: 'Requests', unit: 'req/s', series: [{ name: 'Requests', points: [[Date.parse('2026-11-03T11:00:00Z'), 0.5], [Date.parse('2026-11-03T12:00:00Z'), 1.5]]}] },
    { key: 'errors', title: 'Server errors (5xx)', unit: 'req/s', series: [] },
  ],
  routes: [{ route: 'Season/Get', requests: 1200, errors: 3, p95Ms: 12.3 }, { route: 'Home/Events', requests: 40, errors: 0, p95Ms: null }],
};

const line = (changes: Partial<LogEntryDto> = {}): LogEntryDto => ({
  timestamp: '2026-11-03T11:59:00Z', level: 'info', message: 'HTTP GET /Home/Events responded 200', traceId: 'd8885ad9a3a1121898cdaaaaf596f993',
  spanId: '8b8578a5df7f95eb', source: 'Serilog.AspNetCore.RequestLoggingMiddleware', pod: 'grif-backend-1', attributes: { RequestPath: '/Home/Events' }, ...changes,
});

describe('telemetry page helpers', () => {
  it('orders the waterfall as a tree, children under their parents by start', () => {
    const rows = waterfall(trace([
      span('root', { durationMs: 100 }),
      span('late', { parentSpanId: 'root', offsetMs: 60, durationMs: 40 }),
      span('early', { parentSpanId: 'root', offsetMs: 10, durationMs: 20 }),
      span('query', { parentSpanId: 'early', offsetMs: 12, durationMs: 0 }),
      span('orphan', { parentSpanId: 'not-here', offsetMs: 5 }),
    ]));

    expect(rows.map(r => [r.span.spanId, r.depth])).toEqual([['root', 0], ['early', 1], ['query', 2], ['late', 1], ['orphan', 0]]);
    expect(rows.find(r => r.span.spanId === 'late')).toEqual(jasmine.objectContaining({ left: 60, width: 40 }));
    expect(rows.find(r => r.span.spanId === 'query')!.width).toBeCloseTo(0.3, 10);
    expect(waterfall(trace([span('instant', { durationMs: 0 })], 0))[0]).toEqual(jasmine.objectContaining({ left: 0, width: 0.3 }));
  });

  it('words durations and values', () => {
    expect([0.0123, 2.5, 412.4, 1530, 125000].map(msText)).toEqual(['12 µs', '2.5 ms', '412 ms', '1.53 s', '125 s']);
    expect([0, 0.012345, 1.5, 12.34, 1234.5].map(formatValue)).toEqual(['0', '0.0123', '1.5', '12.3', '1,235']);
  });

  it('rounds the axis up to a round number', () => {
    expect([0, -1, 0.7, 3, 7, 12, 240, 1000].map(niceMax)).toEqual([1, 1, 1, 5, 10, 20, 250, 1000]);
  });
});

describe('TelemetryComponent', () => {
  let fixture: ComponentFixture<TelemetryComponent>;
  let http: HttpTestingController;

  function create(linkedTrace?: string): void {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TelemetryComponent);
    if (linkedTrace)
      fixture.componentRef.setInput('trace', linkedTrace);
    fixture.detectChanges();
  }

  afterEach(() => {
    fixture.destroy();
    http.verify();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  /** The select its <label> names by the label's text. */
  const labelled = (text: string) => [...el().querySelectorAll<HTMLLabelElement>('label.tm-field')]
    .find(l => l.querySelector('.hud-label')?.textContent?.trim() === text)!.querySelector<HTMLSelectElement>('select')!;
  const text = (e: Element | null) => (e?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const button = (label: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find(b => text(b) === label)!;

  /** The request a change made, checked straight after it (a resource still loading keeps the fixture from being stable). */
  async function request(url: string): Promise<TestRequest> {
    await tick();
    return http.expectOne(r => r.url === url);
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  /** A tick: a resource reading another's value starts its request after it. */
  async function tick(): Promise<void> {
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
  }

  /** The status answered, then the Overview's metrics. */
  async function open(status: TelemetryStatusDto = allOn, answer: MetricsDto = metrics): Promise<void> {
    (await request('/api/telemetry/status')).flush(status);
    if (status.metrics)
      (await request('/api/telemetry/metrics')).flush(answer);
    await settle();
  }

  it('charts the overview and lists the busiest routes', async () => {
    create();
    await open();

    expect(Array.from(el().querySelectorAll('.tm-chart-title')).map(text)).toEqual(['Requests req/s', 'Server errors (5xx) req/s']);
    expect(el().querySelectorAll('app-line-chart polyline')).toHaveSize(1);
    expect(text(el().querySelector('app-line-chart .lc-legend'))).toBe('Requests: 1.5 req/s');
    expect(text(el().querySelector('.tm-chart .tm-none'))).toBe('No data in this range.');
    const cells = Array.from(el().querySelectorAll('[aria-label="Busiest routes"] tbody tr')).map(r => Array.from(r.querySelectorAll('td')).map(text));
    expect(cells).toEqual([['Season/Get', '1,200', '3', '12.3 ms'], ['Home/Events', '40', '0', '-']]);
  });

  it('reads the overview again for another range', async () => {
    create();
    await open();

    const select = labelled('Last');
    select.value = '1440';
    select.dispatchEvent(new Event('change'));
    const again = await request('/api/telemetry/metrics');
    expect(again.request.params.get('minutes')).toBe('1440');
    again.flush(metrics);
    await settle();
  });

  it("says which backends aren't set up, and asks them nothing", async () => {
    create();
    await open({ serviceName: 'grif-test', logs: false, metrics: false, traces: false });

    expect(text(el().querySelector('.hud-lede'))).toContain('(grif-test)');
    expect(text(el().querySelector('.hud-empty'))).toContain('TelemetryQuery:PrometheusUrl is empty');
    button('Logs').click();
    await settle();
    expect(text(el().querySelector('.hud-empty'))).toContain('TelemetryQuery:LokiUrl is empty');
    button('Traces').click();
    await settle();
    expect(text(el().querySelector('.hud-empty'))).toContain('TelemetryQuery:TempoUrl is empty');
  });

  it("shows a backend's failure", async () => {
    create();
    (await request('/api/telemetry/status')).flush(allOn);
    (await request('/api/telemetry/metrics')).flush('Prometheus could not be reached: connection refused', { status: 502, statusText: 'Bad Gateway' });
    await settle();

    expect(text(el().querySelector('[role="alert"]'))).toBe('Prometheus could not be reached: connection refused');
  });

  it('filters the logs and opens a line, then its trace', async () => {
    create();
    await open();

    button('Logs').click();
    (await request('/api/telemetry/logs')).flush({ entries: [line(), line({ level: 'error', message: 'Job failed', traceId: null, attributes: {} })], truncated: true });
    await settle();
    expect(Array.from(el().querySelectorAll('.tm-log-line .tm-badge')).map(text)).toEqual(['info', 'error']);
    expect(Array.from(el().querySelectorAll('.tm-log-line .tm-message')).map(text)).toEqual(['HTTP GET /Home/Events responded 200', 'Job failed']);
    expect(text(el().querySelector('.tm-note'))).toContain('The newest 2 lines');

    button('error').click();
    const errors = await request('/api/telemetry/logs');
    expect(errors.request.params.getAll('level')).toEqual(['error']);
    errors.flush({ entries: [line()], truncated: false });
    await settle();

    const search = el().querySelector<HTMLInputElement>('input[aria-label="Search the log lines"]')!;
    search.value = 'events';
    search.dispatchEvent(new Event('input'));
    search.form!.dispatchEvent(new Event('submit'));
    const searched = await request('/api/telemetry/logs');
    expect(searched.request.params.get('search')).toBe('events');
    searched.flush({ entries: [line()], truncated: false });
    await settle();

    el().querySelector<HTMLButtonElement>('.tm-log-line')!.click();
    await settle();
    expect(text(el().querySelector('.tm-props'))).toContain('RequestPath/Home/Events');

    button('Open the trace').click();
    await tick();
    const opened = http.expectOne('/api/telemetry/traces/d8885ad9a3a1121898cdaaaaf596f993');
    http.expectOne(r => r.url === '/api/telemetry/traces').flush([]);
    opened.flush(trace([span('root', { name: 'GET Home/Events', kind: 'Server', durationMs: 100 })]));
    await settle();
    expect(text(el().querySelector('.tm-span-name'))).toBe('GET Home/Events Server');
  });

  it('lists traces, opens one with its spans, and goes to its logs', async () => {
    create();
    await open();

    button('Traces').click();
    const summaries: TraceSummaryDto[] = [
      { traceId: 't1', rootName: 'GET Season/Get', rootService: 'grif-prod', start: '2026-11-03T11:59:00Z', durationMs: 412, route: 'Season/Get', statusCode: 500 },
    ];
    (await request('/api/telemetry/traces')).flush(summaries);
    await settle();
    const cells = Array.from(el().querySelectorAll('[aria-label="Traces"] tbody td')).map(text);
    expect(cells.slice(1)).toEqual(['GET Season/Get', 'Season/Get', '500', '412 ms']);

    const failed = button('Failed only');
    expect(failed.getAttribute('aria-pressed')).toBe('false');
    failed.click();
    const failures = await request('/api/telemetry/traces');
    expect(failures.request.params.get('errorsOnly')).toBe('true');
    failures.flush(summaries);
    await settle();
    expect(failed.getAttribute('aria-pressed')).toBe('true');

    button('GET Season/Get').click();
    (await request('/api/telemetry/traces/t1')).flush(trace([
      span('root', { name: 'GET Season/Get', status: 'Error', statusMessage: 'boom', durationMs: 100,
        events: [{ name: 'exception', offsetMs: 90, attributes: { 'exception.type': 'System.Exception' } }] }),
      span('db', { name: 'SELECT', parentSpanId: 'root', offsetMs: 10, durationMs: 5 }),
    ]));
    await settle();
    expect(Array.from(el().querySelectorAll('.tm-span')).map(b => text(b.querySelector('.tm-span-name')))).toEqual(['GET Season/Get Internal', 'SELECT Internal']);
    expect(el().querySelector('.tm-span')!.classList).toContain('tm-span--error');

    el().querySelector<HTMLButtonElement>('.tm-span')!.click();
    await settle();
    expect(text(el().querySelector('.tm-detail'))).toContain('StatusError: boom');
    expect(text(el().querySelector('.tm-event'))).toBe('exception +90 ms');

    button('Its log lines').click();
    const logs = await request('/api/telemetry/logs');
    expect(logs.request.params.get('traceId')).toBe('t1');
    logs.flush({ entries: [], truncated: false });
    await settle();
    expect(text(el().querySelector('.tm-chip'))).toContain('Trace t1');
    expect(text(el().querySelector('.hud-empty'))).toBe('No log lines match in this range.');
  });

  it('opens the trace a link names', async () => {
    create('ABC');
    (await request('/api/telemetry/status')).flush(allOn);
    await tick();
    const opened = http.expectOne('/api/telemetry/traces/abc');
    opened.flush('No such trace from this app', { status: 404, statusText: 'Not Found' });
    http.expectOne(r => r.url === '/api/telemetry/traces').flush([]);
    await settle();

    expect(text(el().querySelector('.tm-trace [role="alert"]'))).toBe('No such trace from this app');
  });

  it("refreshes each tab, and reads again for each filter", async () => {
    create();
    await open();
    const reply = async (url: string, body: Parameters<TestRequest['flush']>[0]) => {
      const r = await request(url);
      r.flush(body);
      await settle();
      return r.request.params;
    };
    const select = (label: string, value: string) => {
      const s = labelled(label);
      s.value = value;
      s.dispatchEvent(new Event('change'));
    };
    const noLogs = { entries: [], truncated: false };

    button('Refresh').click();
    await reply('/api/telemetry/metrics', metrics);

    button('Logs').click();
    await reply('/api/telemetry/logs', { entries: [line()], truncated: false });
    el().querySelector<HTMLButtonElement>('.tm-log-line')!.click();
    await settle();
    el().querySelector<HTMLButtonElement>('.tm-log-line')!.click();
    await settle();
    expect(el().querySelector('.tm-log .tm-detail')).toBeNull();
    button('warn').click();
    expect((await reply('/api/telemetry/logs', noLogs)).getAll('level')).toEqual(['warn']);
    button('warn').click();
    expect((await reply('/api/telemetry/logs', noLogs)).has('level')).toBeFalse();
    select('Lines', '500');
    expect((await reply('/api/telemetry/logs', noLogs)).get('limit')).toBe('500');
    button('Refresh').click();
    await reply('/api/telemetry/logs', noLogs);
    fixture.componentInstance.logsOfTrace('t9');
    expect((await reply('/api/telemetry/logs', noLogs)).get('traceId')).toBe('t9');
    button("Show every trace's lines").click();
    expect((await reply('/api/telemetry/logs', noLogs)).has('traceId')).toBeFalse();

    button('Traces').click();
    await reply('/api/telemetry/traces', []);
    const slower = el().querySelector<HTMLInputElement>('input[aria-label="Slower than, in milliseconds"]')!;
    slower.value = '250';
    slower.dispatchEvent(new Event('change'));
    expect((await reply('/api/telemetry/traces', [])).get('minDurationMs')).toBe('250');
    slower.value = '';
    slower.dispatchEvent(new Event('change'));
    expect((await reply('/api/telemetry/traces', [])).has('minDurationMs')).toBeFalse();

    const id = el().querySelector<HTMLInputElement>('input[aria-label="Trace id"]')!;
    id.form!.dispatchEvent(new Event('submit'));
    await tick();
    http.expectNone(r => r.url.startsWith('/api/telemetry/traces/'));
    id.value = 'ABC';
    id.dispatchEvent(new Event('input'));
    id.form!.dispatchEvent(new Event('submit'));
    await reply('/api/telemetry/traces/abc', trace([span('root')]));

    button('Refresh').click();
    await tick();
    http.expectOne(r => r.url === '/api/telemetry/traces').flush([]);
    http.expectOne(r => r.url.startsWith('/api/telemetry/traces/')).flush(trace([span('root')]));
    await settle();

    const spanButton = () => el().querySelector<HTMLButtonElement>('.tm-span')!;
    spanButton().click();
    await settle();
    expect(el().querySelector('.tm-trace .tm-detail')).not.toBeNull();
    spanButton().click();
    await settle();
    expect(el().querySelector('.tm-trace .tm-detail')).toBeNull();

    button('Close').click();
    await settle();
    expect(el().querySelector('.tm-trace')).toBeNull();
    expect(fixture.componentInstance.errorText(new Error('x'))).toBe("Couldn't read it.");
  });
});
