import { HttpErrorResponse } from '@angular/common/http';

/**
 * Why the server refused: a ProblemDetails' detail or title (every API error is one), a plain-text message, or the
 * fallback.
 */
export function errorText(e: HttpErrorResponse, fallback: string): string {
  const body: unknown = e.error;
  if (body && typeof body === 'object') {
    const problem = body as { detail?: string | null, title?: string | null };
    return problem.detail || problem.title || fallback;
  }
  return typeof body === 'string' && body.length > 0 ? body : fallback;
}

/** A refusal's reason: the same as errorText (kept for its callers). */
export function problemText(error: HttpErrorResponse, fallback: string): string {
  return errorText(error, fallback);
}
