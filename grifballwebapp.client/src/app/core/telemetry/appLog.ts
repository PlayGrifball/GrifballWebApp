import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Attributes, BrowserLogLevel, BrowserTelemetry } from './browserTelemetry';

/** The most of a cause that goes in a log line's attributes (a body can be large). */
export const MAX_CAUSE = 1000;

/**
 * What a log line says about what went wrong: an HTTP answer's status and path, an error's type and message, or
 * anything else as text.
 */
export function causeAttributes(cause: unknown): Attributes {
  if (cause === undefined)
    return {};
  if (cause instanceof HttpErrorResponse)
    return { 'http.response.status_code': cause.status, 'url.path': urlPath(cause.url), 'error.message': cause.message };
  if (cause instanceof Error)
    return { 'exception.type': cause.name, 'exception.message': cause.message.slice(0, MAX_CAUSE) };
  return { 'error.detail': text(cause).slice(0, MAX_CAUSE) };
}

function urlPath(url: string | null): string | undefined {
  if (!url)
    return undefined;
  try {
    return new URL(url, 'http://localhost').pathname;
  } catch {
    return undefined;
  }
}

function text(value: unknown): string {
  if (typeof value === 'string')
    return value;
  try {
    return JSON.stringify(value) ?? String(value);
  } catch {
    return String(value);
  }
}

/**
 * The app's log: each line goes to the console, as it always has (on the server that is the pod's log), and to the
 * browser's telemetry (BrowserTelemetry, when it is on), with the cause's details as attributes, in the trace of what
 * the user is doing. Use it instead of console.* (docs/frontend.md).
 */
@Injectable({ providedIn: 'root' })
export class AppLog {
  private readonly telemetry = inject(BrowserTelemetry);

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

  /** Detail for following a flow while debugging. */
  debug(message: string, cause?: unknown, attributes?: Attributes): void {
    this.write('debug', message, cause, attributes);
  }

  private write(level: BrowserLogLevel, message: string, cause: unknown, attributes: Attributes = {}): void {
    if (cause === undefined)
      console[level](message);
    else
      console[level](message, cause);
    this.telemetry.log(level, message, { ...causeAttributes(cause), ...attributes });
  }
}
