import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  BrowserTelemetry,
  MAX_BATCH,
  MAX_KEEPALIVE_BYTES,
  MAX_QUEUE,
  TELEMETRY_URL,
  TelemetryBatch,
  formatTraceparent,
  parseTraceparent,
  randomId,
} from './browserTelemetry';

const TRACE = { traceId: '0af7651916cd43dd8448eb211c80319c', spanId: 'b7ad6b7169203331' };

describe('browser telemetry', () => {
  it('makes random hex ids of the asked length, never all zeros', () => {
    expect(randomId(16)).toMatch(/^[0-9a-f]{32}$/);
    expect(randomId(8)).toMatch(/^[0-9a-f]{16}$/);
    expect(randomId(8)).not.toBe(randomId(8));

    let first = true;
    spyOn(crypto, 'getRandomValues').and.callFake(<T extends ArrayBufferView | null>(array: T) => {
      (array as unknown as Uint8Array).fill(first ? 0 : 1);
      first = false;
      return array;
    });
    expect(randomId(8)).toBe('0101010101010101');
  });

  it('reads and writes traceparent', () => {
    expect(parseTraceparent(`00-${TRACE.traceId}-${TRACE.spanId}-01`)).toEqual(TRACE);
    expect(parseTraceparent('00-xyz')).toBeUndefined();
    expect(parseTraceparent(null)).toBeUndefined();
    expect(formatTraceparent(TRACE)).toBe(`00-${TRACE.traceId}-${TRACE.spanId}-01`);
  });

  describe('the service', () => {
    let fetchSpy: jasmine.Spy;
    let webdriver: jasmine.Spy;
    let entries: jasmine.Spy;
    const batches = () => fetchSpy.calls.all().map(c => JSON.parse((c.args[1] as RequestInit).body as string) as TelemetryBatch);
    const settle = async () => { for (let i = 0; i < 5; i++) await Promise.resolve(); };

    beforeEach(() => {
      fetchSpy = spyOn(window, 'fetch').and.callFake(() => Promise.resolve(new Response(null, { status: 204 })));
      webdriver = spyOnProperty(Navigator.prototype, 'webdriver', 'get').and.returnValue(false);
      // The page the app's server sent, with browser telemetry on
      entries = spyOn(performance, 'getEntriesByType').and.returnValue([{ serverTiming: [{ name: 'browser-telemetry', description: '' }] }] as never);
    });

    const telemetry = (platform = 'browser') => {
      TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
      return TestBed.inject(BrowserTelemetry);
    };

    it('records nothing unless the page says browser telemetry is on (its Server-Timing)', () => {
      entries.and.returnValue([{ serverTiming: [] }] as never);
      expect(telemetry().enabled).withContext('no mark').toBeFalse();

      TestBed.resetTestingModule();
      entries.and.returnValue([{ serverTiming: [{ name: 'browser-telemetry', description: '' }] }] as never);
      expect(telemetry().enabled).withContext('marked on').toBeTrue();
    });

    it('measures a batch\'s size for keepalive in bytes, not characters', () => {
      const t = telemetry();
      t.log('info', 'é'.repeat(31_000));

      t.flush();

      expect((fetchSpy.calls.mostRecent().args[1] as RequestInit).keepalive).toBeFalse();
    });

    for (const status of [400, 413])
      it(`drops only the batch the API refused (${status}), and keeps sending`, async () => {
        fetchSpy.and.callFake(() => Promise.resolve(new Response(null, { status })));
        const t = telemetry();
        t.log('info', 'first');
        t.flush();
        await settle();

        t.log('info', 'second');
        t.flush();

        expect(fetchSpy).toHaveBeenCalledTimes(2);
        expect(t.enabled).toBeTrue();
      });

    it('keeps a batch inside the API\'s limits: a span\'s name, a log\'s message, a vital\'s value', () => {
      const t = telemetry();
      t.startSpan('n'.repeat(300), { parent: null }).end();
      t.log('info', 'm'.repeat(5_000));
      t.vital('LCP', 900_000);

      t.flush();

      const [batch] = batches();
      expect(batch.spans[0].name.length).toBeLessThanOrEqual(200);
      expect(batch.logs[0].message.length).toBeLessThanOrEqual(4_000);
      expect(batch.vitals).toEqual([]);
    });

    it('sends a finished span, with its parent, kind, status and set attributes', () => {
      const t = telemetry();
      const span = t.startSpan('navigation', { parent: TRACE, attributes: { a: 1, gone: undefined } });
      span.setName('navigation /season/:seasonID');
      span.setAttribute('http.route', '/season/:seasonID');
      span.setStatus('error');
      span.end();
      span.end();

      t.flush();

      expect(fetchSpy).toHaveBeenCalledOnceWith(TELEMETRY_URL, jasmine.objectContaining({ method: 'POST', keepalive: true, credentials: 'same-origin' }));
      const [sent] = batches()[0].spans;
      expect(batches()[0].spans.length).toBe(1);
      expect(sent).toEqual(jasmine.objectContaining({
        traceId: TRACE.traceId, parentSpanId: TRACE.spanId, name: 'navigation /season/:seasonID', kind: 'internal', status: 'error',
        attributes: { a: 1, 'http.route': '/season/:seasonID' },
      }));
      expect(sent.spanId).toMatch(/^[0-9a-f]{16}$/);
      expect(sent.end).toBeGreaterThanOrEqual(sent.start);
    });

    it('puts a span under the active one unless told otherwise, and starts a trace for null', () => {
      const t = telemetry();
      const navigation = t.startSpan('navigation', { parent: null });
      t.setActive(navigation);

      const child = t.startSpan('GET', { kind: 'client' });
      const own = t.startSpan('GET', { parent: null });

      expect(navigation.parentSpanId).toBeUndefined();
      expect(child.traceId).toBe(navigation.traceId);
      expect(child.parentSpanId).toBe(navigation.spanId);
      expect(own.traceId).not.toBe(navigation.traceId);
      expect(child.traceparent).toBe(`00-${child.traceId}-${child.spanId}-01`);

      navigation.end();
      expect(t.active()).toBeUndefined();
    });

    it('logs in the active span\'s trace, or the one it is given', () => {
      const t = telemetry();
      const navigation = t.startSpan('navigation', { parent: null });
      t.setActive(navigation);
      t.log('info', 'in the navigation', { page: '/teams', none: undefined });
      t.log('warn', 'in its own', {}, TRACE);
      t.setActive(undefined);
      t.log('error', 'in none');

      t.flush();

      const [inNavigation, inOwn, inNone] = batches()[0].logs;
      expect(inNavigation).toEqual(jasmine.objectContaining({ level: 'info', message: 'in the navigation', traceId: navigation.traceId, spanId: navigation.spanId, attributes: { page: '/teams' } }));
      expect(inOwn).toEqual(jasmine.objectContaining({ traceId: TRACE.traceId, spanId: TRACE.spanId }));
      expect(inNone.traceId).toBeUndefined();
    });

    it('sends the Web Vitals', () => {
      const t = telemetry();
      t.vital('LCP', 1250);

      t.flush();

      expect(batches()[0].vitals).toEqual([{ name: 'LCP', value: 1250 }]);
    });

    it('sends nothing when nothing waits', () => {
      telemetry().flush();

      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it(`sends at most ${MAX_BATCH} of each a batch, the rest next time`, () => {
      const t = telemetry();
      for (let i = 0; i < MAX_BATCH + 3; i++)
        t.log('info', `line ${i}`);

      t.flush();
      t.flush();

      expect(batches().map(b => b.logs.length)).toEqual([MAX_BATCH, 3]);
    });

    it(`keeps at most ${MAX_QUEUE} of each waiting, dropping the oldest`, () => {
      const t = telemetry();
      for (let i = 0; i < MAX_QUEUE + 10; i++)
        t.vital('CLS', i);

      while (fetchSpy.calls.count() < 10 && batches().reduce((n, b) => n + b.vitals.length, 0) < MAX_QUEUE)
        t.flush();

      const values = batches().flatMap(b => b.vitals.map(v => v.value));
      expect(values.length).toBe(MAX_QUEUE);
      expect(values[0]).toBe(10);
    });

    it('sends a batch too big for keepalive without it', () => {
      const t = telemetry();
      t.log('info', 'x'.repeat(MAX_KEEPALIVE_BYTES));

      t.flush();

      expect((fetchSpy.calls.mostRecent().args[1] as RequestInit).keepalive).toBeFalse();
    });

    it('sends what waits when the page hides or goes away', () => {
      const t = telemetry();
      t.log('info', 'before hiding');
      spyOnProperty(Document.prototype, 'visibilityState', 'get').and.returnValue('hidden');
      dispatchEvent(new Event('visibilitychange'));
      t.log('info', 'before leaving');
      dispatchEvent(new Event('pagehide'));

      expect(batches().map(b => b.logs[0].message)).toEqual(['before hiding', 'before leaving']);
    });

    for (const status of [404])
      it(`stops once the API says telemetry is off (${status}), and drops what waits`, async () => {
        fetchSpy.and.callFake(() => Promise.resolve(new Response(null, { status })));
        const t = telemetry();
        t.log('info', 'first');
        t.flush();
        await settle();

        t.log('info', 'second');
        t.flush();

        expect(fetchSpy).toHaveBeenCalledTimes(1);
        expect(t.enabled).toBeFalse();
      });

    it('keeps sending after a 429: a limit is no refusal for good', async () => {
      fetchSpy.and.callFake(() => Promise.resolve(new Response(null, { status: 429 })));
      const t = telemetry();
      t.log('info', 'first');
      t.flush();
      await settle();

      t.log('info', 'second');
      t.flush();

      expect(fetchSpy).toHaveBeenCalledTimes(2);
    });

    it('lets a batch fail quietly', async () => {
      fetchSpy.and.callFake(() => Promise.reject(new TypeError('offline')));
      const t = telemetry();
      t.log('info', 'lost');

      t.flush();

      await expectAsync(fetchSpy.calls.mostRecent().returnValue).toBeRejected();
      expect(t.enabled).toBeTrue();
    });

    it('records nothing on the server', () => {
      const t = telemetry('server');
      t.log('error', 'boom');
      t.startSpan('navigation').end();
      t.flush();

      expect(t.enabled).toBeFalse();
      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it('records nothing from an automated browser (tests, Lighthouse)', () => {
      webdriver.and.returnValue(true);
      const t = telemetry();
      t.log('error', 'boom');
      t.vital('LCP', 1);
      t.flush();

      expect(fetchSpy).not.toHaveBeenCalled();
    });
  });
});
