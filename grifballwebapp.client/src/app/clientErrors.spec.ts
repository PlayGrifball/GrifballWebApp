import { HttpClient, HttpErrorResponse, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ErrorHandler, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { redactUrl } from './redactUrl';
import { BrowserTelemetry } from './telemetry/browserTelemetry';
import {
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
  isCancelledByBrowser,
  reportedUrl,
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
    });

    it('leaves out the page\'s once the app has moved to another page in the browser', () => {
      const perf = { getEntriesByType: () => [{ name: 'https://grifball.xyz/season/1', serverTiming: [{ name: 'traceparent', description: TRACEPARENT }] }] } as unknown as Performance;

      expect(pageTraceparent(perf, 'https://grifball.xyz/season/1#top')).toBe(TRACEPARENT);
      expect(pageTraceparent(perf, 'https://grifball.xyz/teams')).toBeUndefined();
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

  it('reports a URL\'s path and query, its sensitive values redacted', () => {
    expect(reportedUrl('https://grifball.xyz/api/Identity/Reset?token=secret&page=2', 'https://grifball.xyz/')).toBe('/api/Identity/Reset?token=REDACTED&page=2');
    expect(reportedUrl('/api/Teams?x=1', 'https://grifball.xyz/')).toBe('/api/Teams?x=1');
    expect(reportedUrl(null, 'https://grifball.xyz/')).toBe('');
    expect(reportedUrl('/' + 'a'.repeat(400), 'https://grifball.xyz/')).toHaveSize(300);
  });

  it('takes a call with no answer for the browser\'s doing while the page goes away or hides, or offline', () => {
    const visible = { visibilityState: 'visible' } as Document;
    const online = { onLine: true } as Navigator;
    expect(isCancelledByBrowser(false, visible, online)).toBeFalse();
    expect(isCancelledByBrowser(true, visible, online)).toBeTrue();
    expect(isCancelledByBrowser(false, { visibilityState: 'hidden' } as Document, online)).toBeTrue();
    expect(isCancelledByBrowser(false, visible, { onLine: false } as Navigator)).toBeTrue();
  });

  it('redacts sensitive query values in an error\'s message and stack', () => {
    const error = new Error('failed to load https://grifball.xyz/reset-password?token=abc123&email=a%40b.c&page=2');
    error.stack = 'Error: x\n    at https://grifball.xyz/main.js?sig=s3cr3t:1:2';

    const described = describeError(error);

    expect(described.message).not.toContain('abc123');
    expect(described.message).not.toContain('a%40b.c');
    expect(described.message).toContain('page=2');
    expect(described.stack).not.toContain('s3cr3t');
    expect(describeError('see /x?code=zzz').message).not.toContain('zzz');
  });

  it('never describes an error with an empty message, which the API refuses', () => {
    expect(describeError('').message.trim()).not.toBe('');
    expect(describeError('   ').message.trim()).not.toBe('');
    expect(describeError(new Error('')).message.trim()).not.toBe('');
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
    expect(describeError('x'.repeat(MAX_MESSAGE + 10)).message).toHaveSize(MAX_MESSAGE);
  });

  it('reports calls that got no answer or a server error, not 4xx', () => {
    expect(isReportedFailure(new HttpErrorResponse({ status: 0 }))).toBeTrue();
    expect(isReportedFailure(new HttpErrorResponse({ status: 503 }))).toBeTrue();
    expect(isReportedFailure(new HttpErrorResponse({ status: 404 }))).toBeFalse();
    expect(isReportedFailure(new HttpErrorResponse({ status: 401 }))).toBeFalse();
  });

  describe('the reporter', () => {
    /** The telemetry the reports become log lines of: enabled unless a test says otherwise. */
    let telemetry: { enabled: boolean; active: jasmine.Spy; log: jasmine.Spy; flush: jasmine.Spy };
    /** Each log line as the report it was. */
    const sent = (): ClientErrorReport[] => telemetry.log.calls.all().map(c => {
      const [, message, attributes, trace] = c.args as [string, string, Record<string, unknown>, { traceId: string; spanId: string } | undefined];
      return {
        kind: attributes['error.kind'] as 'error' | 'http',
        message,
        page: attributes['page'] as string,
        stack: attributes['exception.stacktrace'] as string | undefined,
        method: attributes['http.request.method'] as string | undefined,
        url: attributes['url.path'] as string | undefined,
        status: attributes['http.response.status_code'] as number | undefined,
        traceparent: trace ? `00-${trace.traceId}-${trace.spanId}-01` : undefined,
      };
    });

    beforeEach(() => {
      telemetry = { enabled: true, active: jasmine.createSpy('active').and.returnValue(undefined), log: jasmine.createSpy('log'), flush: jasmine.createSpy('flush') };
    });

    const reporter = (platform = 'browser') => {
      TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }, { provide: BrowserTelemetry, useValue: telemetry }] });
      return TestBed.inject(ClientErrorReporter);
    };

    it('logs an error at error level with its page and stack', () => {
      reporter().reportError(new Error('boom'));

      expect(telemetry.log).toHaveBeenCalledTimes(1);
      expect(telemetry.log.calls.mostRecent().args[0]).toBe('error');
      const report = sent()[0];
      expect(report.kind).toBe('error');
      expect(report.message).toBe('Error: boom');
      expect(report.page).toBe(redactUrl(location.href));
      expect(report.stack).toContain('boom');
    });

    it('logs an error in the navigation under way', () => {
      telemetry.active.and.returnValue({ traceparent: CALL_TRACEPARENT });

      reporter().reportError(new Error('boom'));

      expect(sent()[0].traceparent).toBe(CALL_TRACEPARENT);
    });

    it('logs a failed call with its method, URL, status and trace, the URL\'s token redacted everywhere', () => {
      reporter().reportCall('GET', new HttpErrorResponse({
        status: 502, statusText: 'Bad Gateway', url: 'https://grifball.xyz/api/Teams?token=s3cret&season=4',
        headers: new HttpHeaders({ traceresponse: CALL_TRACEPARENT }),
      }));

      const report = sent()[0];
      expect(report).toEqual(jasmine.objectContaining({
        kind: 'http', method: 'GET', url: '/api/Teams?token=REDACTED&season=4', status: 502, traceparent: CALL_TRACEPARENT,
      }));
      expect(report.message).toBe('Http failure response for /api/Teams?token=REDACTED&season=4: 502 Bad Gateway');
      expect(JSON.stringify(telemetry.log.calls.all().map(c => c.args))).not.toContain('s3cret');
    });

    it('leaves out a call with no answer once the page is going away', () => {
      const r = reporter();
      dispatchEvent(new Event('pagehide'));

      r.reportCall('GET', new HttpErrorResponse({ status: 0, url: '/api/Teams' }));

      expect(telemetry.log).not.toHaveBeenCalled();
    });

    it('sends an error at once as the page goes away, while it still can', () => {
      const r = reporter();
      dispatchEvent(new Event('pagehide'));

      r.reportError(new Error('on unload'));

      expect(telemetry.flush).toHaveBeenCalled();
    });

    it('leaves out a call with no answer while offline, and still logs a 5xx', () => {
      spyOnProperty(Navigator.prototype, 'onLine', 'get').and.returnValue(false);
      const r = reporter();

      r.reportCall('GET', new HttpErrorResponse({ status: 0, url: '/api/Teams' }));
      r.reportCall('GET', new HttpErrorResponse({ status: 503, url: '/api/Teams' }));

      expect(telemetry.log).toHaveBeenCalledTimes(1);
      expect(sent()[0].status).toBe(503);
    });

    it('falls back to the page\'s trace for a call that brought none back', () => {
      spyOn(performance, 'getEntriesByType').and.returnValue([{ serverTiming: [{ name: 'traceparent', description: TRACEPARENT }] }] as never);

      reporter().reportCall('GET', new HttpErrorResponse({ status: 503, url: '/api/Teams' }));

      expect(sent()[0].traceparent).toBe(TRACEPARENT);
    });

    it('logs each error once', () => {
      const r = reporter();
      r.reportError(new Error('same'));
      r.reportError(new Error('same'));
      r.reportError(new Error('other'));

      expect(telemetry.log).toHaveBeenCalledTimes(2);
    });

    it(`logs at most ${MAX_REPORTS} a page load`, () => {
      const r = reporter();
      for (let i = 0; i < MAX_REPORTS + 5; i++)
        r.reportError(new Error(`error ${i}`));

      expect(telemetry.log).toHaveBeenCalledTimes(MAX_REPORTS);
    });

    it('reports a call with no answer again once the page comes back from the back/forward cache', () => {
      const r = reporter();
      dispatchEvent(new Event('pagehide'));
      dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true }));

      r.reportCall('GET', new HttpErrorResponse({ status: 0, url: '/api/Teams' }));

      expect(telemetry.log).toHaveBeenCalledTimes(1);
    });

    it('logs nothing on the server, and reads none of the browser\'s globals there', () => {
      const r = reporter('server');
      r.reportError(new Error('boom'));
      r.reportCall('GET', new HttpErrorResponse({ status: 504, url: 'https://backend/Teams' }));

      expect(telemetry.log).not.toHaveBeenCalled();
    });

    it('logs nothing where the telemetry sends nothing (automated browsers, telemetry off)', () => {
      telemetry.enabled = false;
      reporter().reportError(new Error('boom'));

      expect(telemetry.log).not.toHaveBeenCalled();
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

  it('leaves an HTTP error to the interceptor, which reports it (redacted) once', () => {
    const report = jasmine.createSpyObj<ClientErrorReporter>('reporter', ['reportError']);
    TestBed.configureTestingModule({ providers: [ReportingErrorHandler, { provide: ClientErrorReporter, useValue: report }] });
    spyOn(console, 'error');

    TestBed.inject(ReportingErrorHandler).handleError(new HttpErrorResponse({ status: 500, url: 'https://grifball.xyz/api/x?token=abc' }));

    expect(report.reportError).not.toHaveBeenCalled();
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
