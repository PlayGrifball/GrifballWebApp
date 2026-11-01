import { HttpErrorResponse } from '@angular/common/http';

/** The server's plain-text error message, if it sent one, else the fallback. */
export function errorText(e: HttpErrorResponse, fallback: string): string {
  return typeof e.error === 'string' && e.error.length > 0 ? e.error : fallback;
}

/** A refusal's reason: a ProblemDetails' detail or title, a plain-text message, or the fallback. */
export function problemText(error: HttpErrorResponse, fallback: string): string {
  const body = error.error;
  if (body && typeof body === 'object') {
    const problem = body as { detail?: string | null, title?: string | null };
    return problem.detail || problem.title || fallback;
  }
  return errorText(error, fallback);
}
