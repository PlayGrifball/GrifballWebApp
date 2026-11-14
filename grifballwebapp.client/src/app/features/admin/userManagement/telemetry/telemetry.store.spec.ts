import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LogsQuery, MetricsQuery, TelemetryStore, TracesQuery } from './telemetry.store';

describe('TelemetryStore', () => {
  let store: TelemetryStore;
  let http: HttpTestingController;
  const metricsQuery = signal<MetricsQuery | undefined>(undefined);
  const logsQuery = signal<LogsQuery | undefined>(undefined);
  const tracesQuery = signal<TracesQuery | undefined>(undefined);
  const traceId = signal<string | null>(null);

  beforeEach(async () => {
    metricsQuery.set(undefined);
    logsQuery.set(undefined);
    tracesQuery.set(undefined);
    traceId.set(null);
    TestBed.configureTestingModule({ providers: [TelemetryStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(TelemetryStore);
    http = TestBed.inject(HttpTestingController);
    store.metricsQuery.use(metricsQuery);
    store.logsQuery.use(logsQuery);
    store.tracesQuery.use(tracesQuery);
    store.traceId.use(traceId);
    TestBed.tick();
    http.expectOne('/api/telemetry/status').flush({ logs: true, metrics: true, traces: true });
    await settle();
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  function settle() {
    return TestBed.inject(ApplicationRef).whenStable();
  }

  it('reads which backends there are, and nothing else until asked', () => {
    expect(store.status.value()).toEqual({ logs: true, metrics: true, traces: true } as never);
    http.expectNone(() => true);
  });

  it('reads the metrics asked for, and again on a refresh', async () => {
    metricsQuery.set({ minutes: 60 });
    TestBed.tick();
    http.expectOne(r => r.url === '/api/telemetry/metrics' && r.params.get('minutes') === '60').flush({ series: [] });
    await settle();
    expect(store.metrics.hasValue()).toBeTrue();

    store.reloadMetrics();
    TestBed.tick();
    http.expectOne(r => r.url === '/api/telemetry/metrics').flush({ series: [] });
    await settle();
  });

  it('reads the logs asked for, and again on a refresh', async () => {
    logsQuery.set({ minutes: 15, limit: 200, search: 'boom' });
    TestBed.tick();
    const logs = http.expectOne(r => r.url === '/api/telemetry/logs');
    expect(logs.request.params.get('search')).toBe('boom');
    logs.flush({ entries: [] });
    await settle();

    store.reloadLogs();
    TestBed.tick();
    http.expectOne(r => r.url === '/api/telemetry/logs').flush('x', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(store.logs.error()).toBeTruthy();
  });

  it('reads the traces, and the open trace with them on a refresh', async () => {
    tracesQuery.set({ minutes: 60, errorsOnly: true });
    TestBed.tick();
    http.expectOne(r => r.url === '/api/telemetry/traces' && r.params.get('errorsOnly') === 'true').flush([]);
    await settle();

    store.reloadTraces();
    TestBed.tick();
    http.expectOne(r => r.url === '/api/telemetry/traces').flush([]);
    await settle();

    traceId.set('abc');
    TestBed.tick();
    http.expectOne('/api/telemetry/traces/abc').flush({ traceId: 'abc', durationMs: 5, spans: [] });
    await settle();
    expect(store.traceDetail.hasValue()).toBeTrue();

    store.reloadTraces();
    TestBed.tick();
    http.expectOne(r => r.url === '/api/telemetry/traces').flush([]);
    http.expectOne('/api/telemetry/traces/abc').flush({ traceId: 'abc', durationMs: 5, spans: [] });
    await settle();
  });
});
