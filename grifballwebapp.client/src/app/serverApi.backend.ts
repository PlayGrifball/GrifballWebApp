import { FetchBackend, HttpErrorResponse, HttpEvent, HttpRequest } from '@angular/common/http';
import { Injectable, InjectionToken, REQUEST, inject } from '@angular/core';
import { Observable, TimeoutError, catchError, throwError, timeout } from 'rxjs';

/** Where the backend answers on the server, without the /api prefix (ex. http://grifballwebapp-server:5295). */
export const API_ORIGIN = new InjectionToken<string>('API_ORIGIN');

/**
 * How long the server waits for each API answer before rendering the page without it. The browser
 * then asks for it itself, as the answer is not in the page's transfer cache.
 */
export const SERVER_API_TIMEOUT_MS = 5000;

/**
 * The only methods the server sends while it renders. Rendering a page must change nothing: a POST made
 * there would run once on the server and again in the browser once the page starts, and whatever cookies
 * its answer set would never reach the browser (the server keeps the answer).
 */
export const SERVER_SAFE_METHODS: ReadonlySet<string> = new Set(['GET', 'HEAD']);

/** The page request's headers that go on to the API: the visitor's cookies and address. */
export const FORWARDED_HEADERS = ['cookie', 'x-real-ip', 'x-forwarded-for'];

/**
 * HttpClient's backend while rendering on the server. The app asks for /api/... on its own origin,
 * which the browser reaches through the proxy (server.ts). On the server, Angular has already made
 * those URLs absolute against the page's own address (https://<site>/api/...); this sends them
 * straight to the backend instead, as the proxy would, rather than out to the internet and back.
 * Other URLs go out as they are. Everything before it (interceptors, the transfer cache) still sees
 * the app's own URL, so the browser finds the answers under the URLs it asks for. Anything but a GET or
 * HEAD is refused (SERVER_SAFE_METHODS), whatever its URL.
 */
@Injectable()
export class ServerApiBackend extends FetchBackend {
  private readonly apiOrigin = inject(API_ORIGIN);
  private readonly pageRequest = inject(REQUEST, { optional: true });

  override handle(req: HttpRequest<unknown>): Observable<HttpEvent<unknown>> {
    if (!SERVER_SAFE_METHODS.has(req.method)) {
      console.error(`Refused ${req.method} ${req.url} while rendering on the server: only the browser makes changes`);
      return throwError(() => new HttpErrorResponse({ status: 405, statusText: 'Method Not Allowed', url: req.url }));
    }
    const url = new URL(req.url);
    const page = this.pageRequest ? new URL(this.pageRequest.url) : null;
    if (page === null || url.host !== page.host || !url.pathname.startsWith('/api/'))
      return super.handle(req);

    let headers = req.headers;
    for (const name of FORWARDED_HEADERS) {
      const value = this.pageRequest!.headers.get(name);
      if (value !== null && !headers.has(name))
        headers = headers.set(name, value);
    }
    const apiReq = req.clone({ url: this.apiOrigin + url.pathname.slice('/api'.length) + url.search, headers });
    return super.handle(apiReq).pipe(
      timeout(SERVER_API_TIMEOUT_MS),
      catchError(error => throwError(() => error instanceof TimeoutError
        ? new HttpErrorResponse({ status: 504, statusText: 'Gateway Timeout', url: apiReq.url })
        : error)),
    );
  }
}
