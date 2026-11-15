import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { describeError, pageTraceparent } from '../http/clientErrors';
import { Attributes, BrowserLogLevel, BrowserTelemetry, parseTraceparent } from './browserTelemetry';

/** The most lines of each level a page sends to the telemetry: a loop that logs sends no more, and info can't crowd out an error. */
export const MAX_LOGS = 50;

/**
 * What a log line says about its cause, as the error reports say it (clientErrors.ts): an error's message and stack, or
 * anything else as text, sensitive query values redacted.
 */
export function causeAttributes(cause: unknown): Attributes {
  if (cause === undefined)
    return {};
  const { message, stack } = describeError(cause);
  return cause instanceof Error
    ? { 'exception.message': message, 'exception.stacktrace': stack }
    : { 'error.detail': message };
}

/**
 * The app's log: each line goes to the console, as it always has (on the server that is the pod's log), and, at info
 * and above, to the browser's telemetry (BrowserTelemetry, when it is on) with its cause's details, in the trace of what
 * the user is doing. A failed API call goes to the console only: the call reports (clientErrorsInterceptor) send those
 * worth sending (no answer, or a 5xx) in the call's own trace, and a 4xx is the app working. The telemetry gets each
 * line once a page, and at most MAX_LOGS of each level. Use it instead of console.* (docs/frontend.md).
 */
@Injectable({ providedIn: 'root' })
export class AppLog {
  private readonly telemetry = inject(BrowserTelemetry);
  private readonly sent = new Set<string>();
  private readonly sentOf: Record<BrowserLogLevel, number> = { debug: 0, info: 0, warn: 0, error: 0 };

  /** Something failed (a request the user's action needed, a callback that threw). */
  error(message: string, cause?: unknown, attributes?: Attributes): void {
    this.write('error', message, cause, attributes);
  }

  /** Something unexpected that the page got past (a retry, a missing element). */
  warn(message: string, cause?: unknown, attributes?: Attributes): void {
    this.write('warn', message, cause, attributes);
  }

  /** Something worth knowing happened (a sign-in step, a change saved). */
  info(message: string, cause?: unknown, attributes?: Attributes): void {
    this.write('info', message, cause, attributes);
  }

  /** Detail for following a flow while debugging: the console only. */
  debug(message: string, cause?: unknown, attributes?: Attributes): void {
    this.write('debug', message, cause, attributes);
  }

  private write(level: BrowserLogLevel, message: string, cause: unknown, attributes: Attributes = {}): void {
    if (cause === undefined)
      console[level](message);
    else
      console[level](message, cause);
    if (level === 'debug' || cause instanceof HttpErrorResponse || !this.telemetry.enabled || this.sentOf[level] >= MAX_LOGS)
      return;
    const lineAttributes = { ...causeAttributes(cause), ...attributes };
    // Not the stack: the same failure from another call site is the same line
    const key = JSON.stringify([level, message, { ...lineAttributes, 'exception.stacktrace': undefined }]);
    if (this.sent.has(key))
      return;
    this.sent.add(key);
    this.sentOf[level]++;
    this.telemetry.log(level, message, lineAttributes, this.telemetry.active() ?? parseTraceparent(pageTraceparent()));
  }
}
