import { HttpHeaders, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { from, of, switchMap } from 'rxjs';

/** An API answer fetched by the inline script in index.html before the app had started. */
export interface EarlyResponse {
  status: number;
  contentType: string | null;
  body: string;
}

/** What that script leaves on window.grifEarly. */
export interface EarlyFetches {
  /** When the fetches started (Date.now()). */
  at: number;
  /** Each fetch by path and query (e.g. /api/Home/PastSeasons?pageSize=10&pageNumber=1); null when it failed. */
  responses: Record<string, Promise<EarlyResponse | null> | undefined>;
}

declare global {
  interface Window { grifEarly?: EarlyFetches }
}

/** An early answer older than this is not used: the app asks again. */
export const EARLY_FETCH_MAX_AGE_MS = 15_000;

/**
 * Answers the app's first GET of a URL with the response the inline script in index.html already
 * fetched for it, while the app's scripts were downloading, so the page's data does not wait for
 * the app to start. Each early answer is used once. A failed, unsuccessful, late or mismatched
 * one (a body that is not JSON) is dropped and the request goes out as usual. They were fetched with
 * this browser's own cookies, so they are this user's; signing in or out drops the rest of them
 * (AuthSession.signedInOrOut).
 */
export const earlyFetchInterceptor: HttpInterceptorFn = (req, next) => {
  const early = typeof window === 'undefined' ? undefined : window.grifEarly;
  if (!early || req.method !== 'GET')
    return next(req);

  const url = new URL(req.urlWithParams, document.baseURI);
  const key = url.pathname + url.search;
  const pending = early.responses[key];
  if (!pending)
    return next(req);
  delete early.responses[key];

  if (Date.now() - early.at > EARLY_FETCH_MAX_AGE_MS)
    return next(req);

  return from(pending).pipe(switchMap(response => {
    const reused = response && toHttpResponse(req, response);
    return reused ? of(reused) : next(req);
  }));
};

/** The response as HttpClient would have given it, or null if it cannot stand in for one. */
function toHttpResponse(req: HttpRequest<unknown>, response: EarlyResponse): HttpResponse<unknown> | null {
  if (response.status < 200 || response.status >= 300)
    return null;

  let body: unknown;
  if (req.responseType === 'text') {
    body = response.body;
  } else if (req.responseType === 'json') {
    // As HttpClient: an empty body (e.g. 204 No Content) is null.
    try {
      body = response.body === '' ? null : JSON.parse(response.body);
    } catch {
      return null;
    }
  } else {
    return null;
  }

  return new HttpResponse({
    body,
    status: response.status,
    statusText: 'OK',
    url: req.urlWithParams,
    headers: response.contentType ? new HttpHeaders({ 'Content-Type': response.contentType }) : undefined,
  });
}
