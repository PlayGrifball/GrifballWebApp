import { HttpClient, HttpErrorResponse, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ErrorHandler, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import {
  CLIENT_ERRORS_URL,
  ClientErrorReport,
  ClientErrorReporter,
  MAX_MESSAGE,
  MAX_REPORTS,
  ReportingErrorHandler,
  callTraceparent,
  clientErrorsInterceptor,
  describeError,
  isReportedFailure,
  pageTraceparent,
  pathOf,
  validTraceparent,
} from './clientErrors';

const TRACEPARENT = '00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01';
const CALL_TRACEPARENT = '00-1af7651916cd43dd8448eb211c80319c-c7ad6b7169203331-01';

describe('client errors', () => {
  describe('traces', () => {
    it('takes only a W3C traceparent', () => {
      expect(validTraceparent(TRACEPARENT)).toBe(TRACEPARENT);
      expect(validTraceparent('00-xyz')).toBeUndefined();
      expect(validTraceparent(42)).toBeUndefined();
      expect(validTraceparent(undefined)).toBeUndefined();
    });

    it('reads the page\'s from its Server-Timing', () => {
      const perf = (serverTiming: { name: string; description: string }[]) =>
        ({ getEntriesByType: () => [{ serverTiming }] }) as unknown as Performance;

      expect(pageTraceparent(perf([{ name: 'cache', description: 'hit' }, { name: 'traceparent', description: TRACEPARENT }]))).toBe(TRACEPARENT);
      expect(pageTraceparent(perf([]))).toBeUndefined();
      expect(pageTraceparent({ getEntriesByType: () => [] } as unknown as Performance)).toBeUndefined();
      expect(pageTraceparent(undefined)).toBeUndefined();
    });

    it('reads a failed call\'s from traceresponse, else from its ProblemDetails', () => {
      const withHeader = new HttpErrorResponse({ status: 500, headers: new HttpHeaders({ traceresponse: CALL_TRACEPARENT }), error: { traceId: TRACEPARENT } });
      const withBody = new HttpErrorResponse({ status: 500, error: { traceId: TRACEPARENT } });
      const withNeither = new HttpErrorResponse({ status: 0, error: null });

      expect(callTraceparent(withHeader)).toBe(CALL_TRACEPARENT);
      expect(callTraceparent(withBody)).toBe(TRACEPARENT);
      expect(callTraceparent(withNeither)).toBeUndefined();
    });
  });

  it('keeps a URL\'s path and drops its query', () => {
    expect(pathOf('https://grifball.xyz/api/Identity/Reset?token=secret', 'https://grifball.xyz/')).toBe('/api/Identity/Reset');
    expect(pathOf('/api/Teams?x=1', 'https://grifball.xyz/')).toBe('/api/Teams');
    expect(pathOf(null, 'https://grifball.xyz/')).toBe('');
    expect(pathOf('http://[bad', 'https://grifball.xyz/')).toBe('');
  });

  it('describes whatever was thrown', () => {
    const error = new TypeError('x is undefined');
    expect(describeError(error)).toEqual({ message: 'TypeError: x is undefined', stack: error.stack });
    expect(describeError('plain')).toEqual({ message: 'plain' });
    expect(describeError({ code: 1 })).toEqual({ message: '{"code":1}' });
    expect(describeError(undefined)).toEqual({ message: 'undefined' });
    const circular: Record<string, unknown> = {};
    circular['self'] = circular;
    expect(describeError(circular)).toEqual({ message: '[object Object]' });
    expect(describeError('x'.repeat(MAX_MESSAGE + 10)).message.length).toBe(MAX_MESSAGE);
  });

  it('reports calls that got no answer or a server error, not 4xx', () => {
    expect(isReportedFailure(new HttpErrorResponse({ status: 0 }))).toBeTrue();
    expect(isReportedFailure(new HttpErrorResponse({ status: 503 }))).toBeTrue();
    expect(isReportedFailure(new HttpErrorResponse({ status: 404 }))).toBeFalse();
    expect(isReportedFailure(new HttpErrorResponse({ status: 401 }))).toBeFalse();
  });

  describe('the reporter', () => {
    let fetchSpy: jasmine.Spy;
    let webdriver: jasmine.Spy;
    const sent = () => fetchSpy.calls.all().map(c => JSON.parse((c.args[1] as RequestInit).body as string) as ClientErrorReport);

    beforeEach(() => {
      fetchSpy = spyOn(window, 'fetch').and.callFake(() => Promise.resolve(new Response(null, { status: 204 })));
      webdriver = spyOnProperty(Navigator.prototype, 'webdriver', 'get').and.returnValue(false);
    });

    const reporter = (platform = 'browser') => {
      TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
      return TestBed.inject(ClientErrorReporter);
    };

    it('sends an error with its page and the page\'s trace', () => {
      reporter().reportError(new Error('boom'));

      expect(fetchSpy).toHaveBeenCalledOnceWith(CLIENT_ERRORS_URL, jasmine.objectContaining({ method: 'POST', keepalive: true }));
      const report = sent()[0];
      expect(report.kind).toBe('error');
      expect(report.message).toBe('Error: boom');
      expect(report.page).toBe(location.pathname);
      expect(report.stack).toContain('boom');
    });

    it('sends a failed call with its method, path, status and trace', () => {
      reporter().reportCall('GET', new HttpErrorResponse({
        status: 502, statusText: 'Bad Gateway', url: 'https://grifball.xyz/api/Teams?token=x',
        headers: new HttpHeaders({ traceresponse: CALL_TRACEPARENT }),
      }));

      expect(sent()[0]).toEqual(jasmine.objectContaining({
        kind: 'http', method: 'GET', url: '/api/Teams', status: 502, traceparent: CALL_TRACEPARENT,
      }));
    });

    it('sends each error once', () => {
      const r = reporter();
      r.reportError(new Error('same'));
      r.reportError(new Error('same'));
      r.reportError(new Error('other'));

      expect(fetchSpy).toHaveBeenCalledTimes(2);
    });

    it(`sends at most ${MAX_REPORTS} a page load`, () => {
      const r = reporter();
      for (let i = 0; i < MAX_REPORTS + 5; i++)
        r.reportError(new Error(`error ${i}`));

      expect(fetchSpy).toHaveBeenCalledTimes(MAX_REPORTS);
    });

    it('stops once the API says reports are off', async () => {
      fetchSpy.and.callFake(() => Promise.resolve(new Response(null, { status: 404 })));
      const r = reporter();
      r.reportError(new Error('first'));
      await fetchSpy.calls.mostRecent().returnValue;
      await Promise.resolve();

      r.reportError(new Error('second'));

      expect(fetchSpy).toHaveBeenCalledTimes(1);
    });

    it('lets a report fail quietly', async () => {
      fetchSpy.and.callFake(() => Promise.reject(new TypeError('offline')));
      reporter().reportError(new Error('boom'));

      await expectAsync(fetchSpy.calls.mostRecent().returnValue).toBeRejected();
    });

    it('sends nothing on the server', () => {
      reporter('server').reportError(new Error('boom'));

      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it('sends nothing from an automated browser (tests, Lighthouse)', () => {
      webdriver.and.returnValue(true);
      reporter().reportError(new Error('boom'));

      expect(fetchSpy).not.toHaveBeenCalled();
    });
  });

  it('logs an error to the console as before, and reports it', () => {
    const report = jasmine.createSpyObj<ClientErrorReporter>('reporter', ['reportError']);
    TestBed.configureTestingModule({ providers: [ReportingErrorHandler, { provide: ClientErrorReporter, useValue: report }] });
    const consoleError = spyOn(console, 'error');
    const error = new Error('boom');

    TestBed.inject(ReportingErrorHandler).handleError(error);

    expect(consoleError).toHaveBeenCalled();
    expect(report.reportError).toHaveBeenCalledOnceWith(error);
  });

  describe('the interceptor', () => {
    let report: jasmine.SpyObj<ClientErrorReporter>;
    let http: HttpClient;
    let backend: HttpTestingController;

    beforeEach(() => {
      report = jasmine.createSpyObj<ClientErrorReporter>('reporter', ['reportCall']);
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(withInterceptors([clientErrorsInterceptor])),
          provideHttpClientTesting(),
          { provide: ClientErrorReporter, useValue: report },
        ],
      });
      http = TestBed.inject(HttpClient);
      backend = TestBed.inject(HttpTestingController);
    });

    afterEach(() => backend.verify());

    it('reports a server error and passes it on', () => {
      let failed: unknown;
      http.get('/api/Teams').subscribe({ error: e => failed = e });
      backend.expectOne('/api/Teams').flush('down', { status: 502, statusText: 'Bad Gateway' });

      expect(report.reportCall).toHaveBeenCalledOnceWith('GET', jasmine.any(HttpErrorResponse));
      expect((failed as HttpErrorResponse).status).toBe(502);
    });

    it('leaves a 4xx unreported', () => {
      http.get('/api/Teams').subscribe({ error: () => undefined });
      backend.expectOne('/api/Teams').flush('no', { status: 404, statusText: 'Not Found' });

      expect(report.reportCall).not.toHaveBeenCalled();
    });

    it('reports a call that got no answer', () => {
      let failed: unknown;
      http.get('/api/Teams').subscribe({ error: e => failed = e });
      backend.expectOne('/api/Teams').error(new ProgressEvent('error'));

      expect(report.reportCall).toHaveBeenCalledTimes(1);
      expect((failed as HttpErrorResponse).status).toBe(0);
    });
  });

  it('is the app\'s ErrorHandler', () => {
    const providers = appConfig.providers as { provide?: unknown; useClass?: unknown }[];
    expect(providers.find(p => p?.provide === ErrorHandler)?.useClass).toBe(ReportingErrorHandler);
  });
});
