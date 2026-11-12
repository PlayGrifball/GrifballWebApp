import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { ErrorHandler, Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { catchError, throwError } from 'rxjs';

/**
 * The browser's error reports: unhandled errors and failed API calls, sent to the API (POST /api/client-errors,
 * GrifballWebApp.Server/Telemetry/ClientErrors.cs), which logs them with their page and trace id. Each carries
 * the trace it belongs to: a failed call's own (its traceresponse header, or the ProblemDetails' traceId), or
 * else the page's (the Server-Timing header the app's server sends, src/server.telemetry.ts).
 *
 * Nothing is sent on the server, in automated browsers (tests, Lighthouse), or once the API answers 404 (reports
 * are off there). A page load sends at most MAX_REPORTS, and each error once.
 */
export const CLIENT_ERRORS_URL = '/api/client-errors';
export const MAX_REPORTS = 10;
export const MAX_MESSAGE = 1000;
export const MAX_STACK = 4000;
export const MAX_PATH = 300;

/** What the API takes (ClientErrorDto). */
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
export function pageTraceparent(perf: Pick<Performance, 'getEntriesByType'> | undefined = globalThis.performance): string | undefined {
  const navigation = perf?.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
  return validTraceparent(navigation?.serverTiming?.find(t => t.name === 'traceparent')?.description);
}

/** A failed call's trace: its traceresponse header, else the traceId of the ProblemDetails it answered with. */
export function callTraceparent(error: HttpErrorResponse): string | undefined {
  return validTraceparent(error.headers?.get('traceresponse'))
    ?? validTraceparent((error.error as { traceId?: unknown } | null)?.traceId);
}

/** A URL's path alone: a query string can carry tokens (password resets, sign-in callbacks). */
export function pathOf(url: string | null | undefined, base: string): string {
  if (!url)
    return '';
  try {
    return new URL(url, base).pathname.slice(0, MAX_PATH);
  } catch {
    return '';
  }
}

/** An error's message and stack, whatever was thrown. */
export function describeError(error: unknown): { message: string; stack?: string } {
  if (error instanceof Error)
    return { message: `${error.name}: ${error.message}`.slice(0, MAX_MESSAGE), stack: error.stack?.slice(0, MAX_STACK) };
  let message: string;
  try {
    message = typeof error === 'string' ? error : JSON.stringify(error) ?? String(error);
  } catch {
    message = String(error);
  }
  return { message: message.slice(0, MAX_MESSAGE) };
}

/** Whether a failed call is worth reporting: no answer at all, or a server error. 4xx is the app working. */
export function isReportedFailure(error: HttpErrorResponse): boolean {
  return error.status === 0 || error.status >= 500;
}

@Injectable({ providedIn: 'root' })
export class ClientErrorReporter {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private off = false;
  private sent = 0;
  private readonly seen = new Set<string>();

  /** An unhandled error, in the page's trace. */
  reportError(error: unknown): void {
    this.send({ kind: 'error', ...describeError(error), page: this.page(), traceparent: pageTraceparent() });
  }

  /** A failed API call, in its own trace (or the page's). */
  reportCall(method: string, error: HttpErrorResponse): void {
    this.send({
      kind: 'http',
      message: error.message.slice(0, MAX_MESSAGE),
      page: this.page(),
      traceparent: callTraceparent(error) ?? pageTraceparent(),
      method,
      url: pathOf(error.url, location.href),
      status: error.status,
    });
  }

  private page(): string {
    return location.pathname.slice(0, MAX_PATH);
  }

  private send(report: ClientErrorReport): void {
    // navigator.webdriver: a browser under automation (Playwright, Lighthouse)
    if (!this.browser || navigator.webdriver || this.off || this.sent >= MAX_REPORTS)
      return;
    const key = `${report.kind}|${report.message}|${report.url ?? ''}|${report.page}`;
    if (this.seen.has(key))
      return;
    this.seen.add(key);
    this.sent++;
    // fetch, not HttpClient: a report must never go through the interceptors that report failures. keepalive:
    // it still goes when the error is the page going away.
    fetch(CLIENT_ERRORS_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(report),
      keepalive: true,
      credentials: 'same-origin',
    }).then(response => {
      if (response.status === 404)
        this.off = true;
    }).catch(() => {
      // Reporting is best effort: a failed report is not reported
    });
  }
}

/** Angular's ErrorHandler, which logs to the console as it did, and reports the error. */
@Injectable()
export class ReportingErrorHandler extends ErrorHandler {
  private readonly reporter = inject(ClientErrorReporter);

  override handleError(error: unknown): void {
    super.handleError(error);
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
