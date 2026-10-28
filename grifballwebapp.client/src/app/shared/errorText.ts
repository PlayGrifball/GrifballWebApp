import { HttpErrorResponse } from '@angular/common/http';

/** The server's plain-text error message, if it sent one, else the fallback. */
export function errorText(e: HttpErrorResponse, fallback: string): string {
  return typeof e.error === 'string' && e.error.length > 0 ? e.error : fallback;
}
