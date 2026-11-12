import { HttpClient, HttpRequest, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, PLATFORM_ID, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { BrowserTelemetry, SpanData, TELEMETRY_URL, TelemetryBatch } from './browserTelemetry';
import { isApiCall, provideBrowserTelemetry, routeTemplate, tracingInterceptor } from './browserTracing';

const PAGE_TRACEPARENT = '00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01';

@Component({ template: '' })
class Blank {}

describe('browser tracing', () => {
  let fetchSpy: jasmine.Spy;
  const sentSpans = (): SpanData[] => fetchSpy.calls.all().flatMap(c => (JSON.parse((c.args[1] as RequestInit).body as string) as TelemetryBatch).spans);

  beforeEach(() => {
    fetchSpy = spyOn(window, 'fetch').and.callFake(() => Promise.resolve(new Response(null, { status: 204 })));
    spyOnProperty(Navigator.prototype, 'webdriver', 'get').and.returnValue(false);
  });

  it('traces calls to this app\'s API only', () => {
    const base = 'https://grifball.xyz/season/1';
    expect(isApiCall('/api/Teams', base)).toBeTrue();
    expect(isApiCall('https://grifball.xyz/api/Teams?x=1', base)).toBeTrue();
    expect(isApiCall('https://other.example/api/Teams', base)).toBeFalse();
    expect(isApiCall('/assets/x.json', base)).toBeFalse();
    expect(isApiCall(TELEMETRY_URL, base)).toBeFalse();
    expect(isApiCall('http://[bad', base)).toBeFalse();
  });

  it('names a route as the app\'s routes do', () => {
    const child = { routeConfig: { path: ':seasonID' }, firstChild: null } as unknown as ActivatedRouteSnapshot;
    const parent = { routeConfig: { path: 'season' }, firstChild: child } as unknown as ActivatedRouteSnapshot;
    const empty = { routeConfig: { path: '' }, firstChild: parent } as unknown as ActivatedRouteSnapshot;
    const root = { firstChild: empty } as unknown as ActivatedRouteSnapshot;

    expect(routeTemplate(root)).toBe('/season/:seasonID');
    expect(routeTemplate({ firstChild: null } as unknown as ActivatedRouteSnapshot)).toBe('/');
  });

  describe('the interceptor', () => {
    let http: HttpClient;
    let backend: HttpTestingController;
    let telemetry: BrowserTelemetry;

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideHttpClient(withInterceptors([tracingInterceptor])), provideHttpClientTesting(), { provide: PLATFORM_ID, useValue: 'browser' }],
      });
      http = TestBed.inject(HttpClient);
      backend = TestBed.inject(HttpTestingController);
      telemetry = TestBed.inject(BrowserTelemetry);
    });

    afterEach(() => backend.verify());

    it('sends a call\'s traceparent and records its span, under the navigation under way', () => {
      const navigation = telemetry.startSpan('navigation', { parent: null });
      telemetry.setActive(navigation);

      http.get('/api/Teams?token=s3cret').subscribe();
      const request = backend.expectOne('/api/Teams?token=s3cret');
      const traceparent = request.request.headers.get('traceparent')!;
      request.flush([]);
      telemetry.flush();

      const [span] = sentSpans();
      expect(traceparent).toBe(`00-${span.traceId}-${span.spanId}-01`);
      expect(span).toEqual(jasmine.objectContaining({ name: 'GET', kind: 'client', traceId: navigation.traceId, parentSpanId: navigation.spanId, status: 'unset' }));
      expect(span.attributes).toEqual({ 'http.request.method': 'GET', 'url.path': '/api/Teams?token=REDACTED', 'grif.request.kind': 'api', 'http.response.status_code': 200 });
    });

    it('marks a 5xx an error, and a 4xx not', () => {
      http.get('/api/a').subscribe({ error: () => undefined });
      backend.expectOne('/api/a').flush('down', { status: 502, statusText: 'Bad Gateway' });
      http.get('/api/b').subscribe({ error: () => undefined });
      backend.expectOne('/api/b').flush('no', { status: 404, statusText: 'Not Found' });
      telemetry.flush();

      expect(sentSpans().map(s => [s.status, s.attributes['http.response.status_code']])).toEqual([['error', 502], ['unset', 404]]);
    });

    it('marks a call the browser cancelled (no answer while hidden) as cancelled, not an error', () => {
      spyOnProperty(Document.prototype, 'visibilityState', 'get').and.returnValue('hidden');

      http.get('/api/a').subscribe({ error: () => undefined });
      backend.expectOne('/api/a').error(new ProgressEvent('abort'));
      telemetry.flush();

      expect(sentSpans()[0]).toEqual(jasmine.objectContaining({ status: 'unset', attributes: jasmine.objectContaining({ 'grif.cancelled': true }) }));
    });

    it('marks a call that failed without an HTTP error an error', () => {
      const failing = TestBed.runInInjectionContext(() =>
        tracingInterceptor(new HttpRequest('GET', '/api/a'), () => throwError(() => new Error('broken'))));
      failing.subscribe({ error: () => undefined });
      telemetry.flush();

      expect(sentSpans()[0].status).toBe('error');
    });

    it('leaves other hosts\' calls alone', () => {
      http.get('https://other.example/api/x').subscribe();
      const request = backend.expectOne('https://other.example/api/x');
      expect(request.request.headers.has('traceparent')).toBeFalse();
      request.flush({});
    });
  });

  describe('navigations', () => {
    let router: Router;
    let telemetry: BrowserTelemetry;

    beforeEach(() => {
      spyOn(performance, 'getEntriesByType').and.callFake(((type: string) => type === 'navigation'
        ? [{ serverTiming: [{ name: 'traceparent', description: PAGE_TRACEPARENT }] }]
        : []) as unknown as Performance['getEntriesByType']);
      TestBed.configureTestingModule({
        providers: [
          { provide: PLATFORM_ID, useValue: 'browser' },
          provideRouter([
            { path: 'season/:seasonID', component: Blank },
            { path: 'refused', component: Blank, canActivate: [() => false] },
            { path: 'broken', component: Blank, canActivate: [() => { throw new Error('guard failed'); }] },
            // A guard that ends the navigation's span itself: its NavigationEnd finds none
            { path: 'ended', component: Blank, canActivate: [() => { inject(BrowserTelemetry).active()?.end(); return true; }] },
          ]),
          provideBrowserTelemetry(),
        ],
      });
      router = TestBed.inject(Router);
      telemetry = TestBed.inject(BrowserTelemetry);
    });

    it('trace each navigation by its route, the first in the server\'s page trace', async () => {
      await router.navigateByUrl('/season/12?token=s3cret');
      await router.navigateByUrl('/season/13');
      // Each ends once the app is stable again
      await new Promise(resolve => setTimeout(resolve));
      telemetry.flush();

      const [first, second] = sentSpans();
      expect(first).toEqual(jasmine.objectContaining({ name: 'navigation /season/:seasonID', traceId: '0af7651916cd43dd8448eb211c80319c', parentSpanId: 'b7ad6b7169203331' }));
      expect(first.attributes).toEqual(jasmine.objectContaining({ 'grif.request.kind': 'navigation', 'http.route': '/season/:seasonID', 'url.path': '/season/12?token=REDACTED' }));
      expect(second.traceId).not.toBe(first.traceId);
      expect(second.parentSpanId).toBeUndefined();
    });

    it('start a trace of its own for the first when the page has none, and leave a span already ended as it is', async () => {
      (performance.getEntriesByType as jasmine.Spy).and.returnValue([]);

      await router.navigateByUrl('/ended');
      telemetry.flush();

      const [ended] = sentSpans();
      expect(ended.parentSpanId).toBeUndefined();
      expect(ended.name).toBe('navigation');
    });

    it('end a navigation a guard refused as cancelled, and one that failed as an error', async () => {
      await router.navigateByUrl('/refused');
      await router.navigateByUrl('/broken').catch(() => undefined);
      telemetry.flush();

      const [refused, broken] = sentSpans();
      expect(refused).toEqual(jasmine.objectContaining({ name: 'navigation', status: 'unset', attributes: jasmine.objectContaining({ 'grif.cancelled': true }) }));
      expect(broken).toEqual(jasmine.objectContaining({ name: 'navigation', status: 'error' }));
    });
  });
});
