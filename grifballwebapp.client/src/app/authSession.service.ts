import { HttpBackend, HttpRequest, HttpResponse } from '@angular/common/http';
import { Injectable, Injector, inject } from '@angular/core';
import { Observable, Subject, catchError, filter, map, of, shareReplay } from 'rxjs';

/** Where the antiforgery request token comes from (IdentityController.Antiforgery). */
export const ANTIFORGERY_URL = '/api/Identity/Antiforgery';

/**
 * The browser's session as the interceptors and the SignalR connection see it. The session itself is an
 * HttpOnly cookie the app cannot read; this keeps what goes with it: the antiforgery token for changes,
 * and a signal when someone signs in or out. No dependencies on the app's other services, so the HTTP
 * interceptors can use it while AccountService is still being made.
 */
@Injectable({ providedIn: 'root' })
export class AuthSession {
  // HttpClient's backend, looked up when first needed (not every user of this has HttpClient)
  private readonly injector = inject(Injector);
  private token$: Observable<string | null> | null = null;
  private readonly changes = new Subject<void>();

  /** Emits after every sign-in and sign-out. */
  readonly changed$ = this.changes.asObservable();

  /**
   * The antiforgery request token, fetched once per session and shared by the requests that need it.
   * null if it could not be fetched: the request goes without it (and the API says no if it needed it).
   */
  antiforgeryToken(): Observable<string | null> {
    this.token$ ??= this.injector.get(HttpBackend).handle(new HttpRequest<unknown>('GET', ANTIFORGERY_URL)).pipe(
      filter((event): event is HttpResponse<{ token?: string } | null> => event instanceof HttpResponse),
      map(response => response.body?.token ?? null),
      catchError(() => {
        this.token$ = null;
        return of(null);
      }),
      shareReplay(1),
    );
    return this.token$;
  }

  /** Forgets the antiforgery token, which belongs to the user it was made for; the next change fetches another. */
  dropAntiforgeryToken(): void {
    this.token$ = null;
  }

  /**
   * Someone signed in or out: a new antiforgery token is needed, and the answers index.html fetched before
   * the app started (window.grifEarly) were for the previous session, so they are dropped.
   */
  signedInOrOut(): void {
    this.dropAntiforgeryToken();
    if (typeof window !== 'undefined')
      delete window.grifEarly;
    this.changes.next();
  }
}
