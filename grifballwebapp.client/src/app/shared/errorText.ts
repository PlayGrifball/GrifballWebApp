import { HttpErrorResponse } from '@angular/common/http';

/** A ProblemDetails' detail or title, if the body is one (an object, or its JSON text when the request read text). */
function problemReason(body: unknown): string | null {
  let problem = body;
  if (typeof body === 'string' && body.trimStart().startsWith('{')) {
    try {
      problem = JSON.parse(body);
    } catch {
      return null;
    }
  }
  if (problem && typeof problem === 'object') {
    const { detail, title } = problem as { detail?: string | null, title?: string | null };
    return detail || title || null;
  }
  return null;
}

/**
 * Why the server refused: a ProblemDetails' detail or title (every API error is one), a plain-text message, or the
 * fallback.
 */
export function errorText(e: HttpErrorResponse, fallback: string): string {
  const body: unknown = e.error;
  if (body && typeof body === 'object')
    return problemReason(body) ?? fallback;
  if (typeof body !== 'string' || body.length === 0)
    return fallback;
  return body.trimStart().startsWith('{') ? problemReason(body) ?? fallback : body;
}

/** A refusal's reason: the same as errorText (kept for its callers). */
export function problemText(error: HttpErrorResponse, fallback: string): string {
  return errorText(error, fallback);
}
