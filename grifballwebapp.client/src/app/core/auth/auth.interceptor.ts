import { type HttpInterceptorFn, type HttpRequest, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthSession } from './authSession.service';

/** The header the API reads the antiforgery token from (BrowserAuth.AntiforgeryHeader on the server). */
export const ANTIFORGERY_HEADER = 'X-XSRF-TOKEN';

/** On the API's 400 for a missing or stale antiforgery token (BrowserAuth.AntiforgeryRejectedHeader). */
export const ANTIFORGERY_REJECTED_HEADER = 'X-Antiforgery-Rejected';

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

/**
 * The session is the browser's HttpOnly cookie, which goes with every /api request by itself. What
 * this adds: the antiforgery token on every request that changes something (the API refuses those
 * without it, CookieAntiforgeryFilter). If the API says the token is stale (it belongs to whoever was
 * signed in when it was fetched), it fetches a new one and tries once more.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (SAFE_METHODS.has(req.method) || !isOwnApi(req.url))
    return next(req);

  const session = inject(AuthSession);
  const send = (retry: boolean): ReturnType<typeof next> => session.antiforgeryToken().pipe(
    switchMap(token => next(withToken(req, token))),
    catchError((err: unknown) => {
      if (!retry || !(err instanceof HttpErrorResponse && err.status === 400 && err.headers.has(ANTIFORGERY_REJECTED_HEADER)))
        return throwError(() => err);
      session.dropAntiforgeryToken();
      return send(false);
    }),
  );
  return send(true);
};

/** The app's own API (/api/... or api/..., relative to the page): the token goes nowhere else. */
function isOwnApi(url: string): boolean {
  return url.startsWith('/api/') || url.startsWith('api/');
}

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token === null ? req : req.clone({ setHeaders: { [ANTIFORGERY_HEADER]: token } });
}
