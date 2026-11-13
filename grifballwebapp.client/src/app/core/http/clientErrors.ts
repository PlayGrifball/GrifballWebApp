import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { ErrorHandler, Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { catchError, throwError } from 'rxjs';
import { redactUrl, redactUrlIn, redactUrlsIn } from './redactUrl';
import { BrowserTelemetry, parseTraceparent } from '../telemetry/browserTelemetry';

/**
 * The browser's error reports: unhandled errors and failed API calls, as error log lines of the browser's telemetry
 * (telemetry/browserTelemetry.ts), with their page and trace. Each is in the trace it belongs to: a failed call's own
 * (its traceresponse header, or the ProblemDetails' traceId), or else the navigation under way, or else the page's
 * (the Server-Timing header the app's server sends, src/server.telemetry.ts).
 *
 * Nothing is reported where the telemetry sends nothing (the server, automated browsers, telemetry off). A page load
 * reports at most MAX_REPORTS, and each error once.
 */
export const MAX_REPORTS = 10;
export const MAX_MESSAGE = 1000;
export const MAX_STACK = 4000;
export const MAX_PATH = 300;

/** A report, before it becomes a log line. */
export interface ClientErrorReport {
  kind: 'error' | 'http';
  message: string;
  stack?: string;
  page: string;
  traceparent?: string;
  method?: string;
  url?: string;
  status?: number;
}

const TRACEPARENT = /^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$/;

/** A W3C traceparent, or undefined for anything else. */
export function validTraceparent(value: unknown): string | undefined {
  return typeof value === 'string' && TRACEPARENT.test(value) ? value : undefined;
}

/** The page's trace, from its Server-Timing header: undefined for a page the server did not trace. */
export function pageTraceparent(perf: Pick<Performance, 'getEntriesByType'> | undefined = globalThis.performance,
  currentUrl: string | undefined = globalThis.location?.href): string | undefined {
  const navigation = perf?.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
  // The server traced the page it sent; once the app has moved on in the browser, that trace is another page's
  if (navigation?.name && currentUrl && withoutHash(navigation.name) !== withoutHash(currentUrl))
    return undefined;
  return validTraceparent(navigation?.serverTiming?.find(t => t.name === 'traceparent')?.description);
}

function withoutHash(url: string): string {
  const hash = url.indexOf('#');
  return hash < 0 ? url : url.slice(0, hash);
}

/** A failed call's trace: its traceresponse header, else the traceId of the ProblemDetails it answered with. */
export function callTraceparent(error: HttpErrorResponse): string | undefined {
  return validTraceparent(error.headers?.get('traceresponse'))
    ?? validTraceparent((error.error as { traceId?: unknown } | null)?.traceId);
}

/** A URL as a report carries it: its path and query, the query's sensitive values redacted (redactUrl.ts). */
export function reportedUrl(url: string | null | undefined, base: string): string {
  return redactUrl(url, base).slice(0, MAX_PATH);
}

/** An error's message and stack, whatever was thrown, sensitive query values in them redacted (a URL in a message, a script's). */
export function describeError(error: unknown): { message: string; stack?: string } {
  if (error instanceof Error) {
    const stack = error.stack === undefined ? undefined : redactUrlsIn(error.stack).slice(0, MAX_STACK);
    return { message: redactUrlsIn(`${error.name}: ${error.message.trim() || NO_MESSAGE}`).slice(0, MAX_MESSAGE), stack };
  }
  let message: string;
  try {
    message = typeof error === 'string' ? error : JSON.stringify(error) ?? String(error);
  } catch {
    message = String(error);
  }
  // The API takes no blank message, and would turn reports off for the page
  return { message: redactUrlsIn(message.trim() || NO_MESSAGE).slice(0, MAX_MESSAGE) };
}

/** What a report says for an error thrown without a message (`throw ''`). */
export const NO_MESSAGE = '(no message)';

/** Whether a failed call is worth reporting: no answer at all, or a server error. 4xx is the app working. */
export function isReportedFailure(error: HttpErrorResponse): boolean {
  return error.status === 0 || error.status >= 500;
}

/**
 * Whether a call with no answer (status 0) is the browser's doing rather than the server's: the page is going
 * away or hidden (a reload or a link out cancels what is in flight), or the browser is offline.
 */
export function isCancelledByBrowser(leaving: boolean, doc: Pick<Document, 'visibilityState'>, nav: Pick<Navigator, 'onLine'>): boolean {
  return leaving || doc.visibilityState === 'hidden' || !nav.onLine;
}

@Injectable({ providedIn: 'root' })
export class ClientErrorReporter {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly telemetry = inject(BrowserTelemetry);
  private sent = 0;
  private readonly seen = new Set<string>();
  private leaving = false;

  constructor() {
    if (this.browser) {
      addEventListener('pagehide', () => this.leaving = true);
      // Back from the back/forward cache: the page is live again
      addEventListener('pageshow', () => this.leaving = false);
    }
  }

  /** An unhandled error, in the navigation's trace, or else the page's. */
  reportError(error: unknown): void {
    if (!this.canSend())
      return;
    this.send({ kind: 'error', ...describeError(error), page: this.page(), traceparent: this.telemetry.active()?.traceparent ?? pageTraceparent() });
  }

  /** A failed API call, in its own trace (or the page's). The URL's sensitive query values are redacted, in its message too. */
  reportCall(method: string, error: HttpErrorResponse): void {
    if (!this.canSend() || (error.status === 0 && isCancelledByBrowser(this.leaving, document, navigator)))
      return;
    this.send({
      kind: 'http',
      message: redactUrlIn(error.message, error.url, location.href).slice(0, MAX_MESSAGE),
      page: this.page(),
      traceparent: callTraceparent(error) ?? pageTraceparent(),
      method,
      url: reportedUrl(error.url, location.href),
      status: error.status,
    });
  }

  /** Checked before anything reads the browser's globals (location, document): on the server there are none. */
  private canSend(): boolean {
    return this.browser && this.telemetry.enabled && this.sent < MAX_REPORTS;
  }

  private page(): string {
    return reportedUrl(location.href, location.href);
  }

  private send(report: ClientErrorReport): void {
    const key = `${report.kind}|${report.message}|${report.url ?? ''}|${report.page}`;
    if (this.seen.has(key))
      return;
    this.seen.add(key);
    this.sent++;
    this.telemetry.log('error', report.message, {
      'error.kind': report.kind,
      page: report.page,
      'exception.stacktrace': report.stack,
      'http.request.method': report.method,
      'url.path': report.url,
      'http.response.status_code': report.status,
    }, parseTraceparent(report.traceparent));
    // An error as the page goes away: sent now, while it still can be
    if (this.leaving)
      this.telemetry.flush();
  }
}

/** Angular's ErrorHandler, which logs to the console as it did, and reports the error. */
@Injectable()
export class ReportingErrorHandler extends ErrorHandler {
  private readonly reporter = inject(ClientErrorReporter);

  override handleError(error: unknown): void {
    super.handleError(error);
    // A failed call is the interceptor's to report (redacted, and only no answer or a 5xx)
    if (!(error instanceof HttpErrorResponse))
      this.reporter.reportError(error);
  }
}

/** Reports the app's failed API calls (no answer, or 5xx), and passes the error on as it was. */
export const clientErrorsInterceptor: HttpInterceptorFn = (req, next) => {
  const reporter = inject(ClientErrorReporter);
  return next(req).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse && isReportedFailure(error))
      reporter.reportCall(req.method, error);
    return throwError(() => error);
  }));
};
