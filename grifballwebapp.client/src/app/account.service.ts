import { Injectable, PLATFORM_ID, TransferState, computed, inject, makeStateKey, signal } from '@angular/core';
import { isPlatformBrowser, isPlatformServer } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, ReplaySubject, take } from 'rxjs';
import { MetaInfoResponse } from './accessTokenResponse';
import { LoginDto } from './api/dtos/loginDto';
import { RegisterDto } from './api/dtos/registerDto';
import { GeneratePasswordResetLinkRequestDto, GeneratePasswordResetLinkResponseDto, UsePasswordResetLinkRequestDto } from './api/dtos/passwordResetDtos';
import { AuthSession } from './authSession.service';
import { Notifier } from './shared/notifier.service';

/** Who is signed in (IdentityController.Me): the user, or 204 No Content (null) for a visitor. */
export const ME_URL = '/api/Identity/Me';

/**
 * The user the server rendered the page for (null: a visitor), sent with the page so the browser starts
 * with the same header the server drew, instead of a visitor's until its own call returns.
 */
export const USER_STATE_KEY = makeStateKey<MetaInfoResponse | null>('grifUser');

/** Where earlier versions kept the session (tokens, then the user): removed on the first load. */
export const LEGACY_STORAGE_KEYS = ['access_token', 'metaInfo'] as const;

/**
 * Who is signed in. The session is an HttpOnly cookie that scripts cannot read (so an XSS bug cannot
 * steal it); the app asks the API who it belongs to. On the server the page's cookie goes with that
 * call (ServerApiBackend), so a signed-in user's page is rendered as theirs.
 */
@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);
  private readonly session = inject(AuthSession);
  private readonly transferState = inject(TransferState);
  private readonly isServer = isPlatformServer(inject(PLATFORM_ID));

  /** The signed-in user, null for a visitor, undefined until the API has said. */
  readonly user = signal<MetaInfoResponse | null | undefined>(undefined);
  private known = new ReplaySubject<MetaInfoResponse | null>(1);

  readonly isLoggedIn = computed(() => !!this.user());
  readonly isSysAdmin = computed(() => this.user()?.isSysAdmin ?? false);
  readonly isEventOrganizer = computed(() => this.user()?.isCommissioner ?? false);
  readonly isPlayer = computed(() => this.user()?.isPlayer ?? false);
  readonly personID = computed(() => this.user()?.userID ?? null);
  readonly displayName = computed(() => this.user()?.displayName ?? null);

  constructor() {
    if (isPlatformBrowser(inject(PLATFORM_ID)))
      clearLegacyStorage();

    // The server already asked: take its answer (once; a later sign-in asks again).
    if (this.transferState.hasKey(USER_STATE_KEY)) {
      this.setUser(this.transferState.get(USER_STATE_KEY, null));
      this.transferState.remove(USER_STATE_KEY);
    } else {
      this.refreshUser();
    }
  }

  /** The user once known (null for a visitor): for guards, which must not decide before then. */
  whenKnown(): Observable<MetaInfoResponse | null> {
    return this.known.pipe(take(1));
  }

  /** Asks the API who is signed in. */
  refreshUser(): void {
    // Not in the HTTP transfer cache: the user goes with the page under USER_STATE_KEY instead.
    this.http.get<MetaInfoResponse | null>(ME_URL, { transferCache: false }).subscribe({
      next: user => {
        this.setUser(user ?? null);
        if (this.isServer)
          this.transferState.set(USER_STATE_KEY, user ?? null);
      },
      // Rendered as a visitor then; with nothing sent along, the browser asks again itself.
      error: () => this.setUser(null),
    });
  }

  register(registerDto: RegisterDto) {
    return this.http.post('/api/identity/register', registerDto);
  }

  login(loginDto: LoginDto): void {
    this.http.post('/api/identity/login', loginDto, { responseType: 'text' }).subscribe({
      error: () => this.notifier.open("Login failed", "Close"),
      next: () => this.onSignedIn(),
    });
  }

  /** Back from Discord: turns its sign-in into the session cookie, then goes on to `followUp` if given. */
  loginExternal(followUp: string): void {
    this.http.get('/api/Identity/ExternalLoginCallback', { responseType: 'text' }).subscribe({
      error: () => this.notifier.open("Login failed", "Close"),
      next: () => this.onSignedIn(followUp),
    });
  }

  private onSignedIn(followUp?: string): void {
    this.session.signedInOrOut();
    this.user.set(undefined);
    this.known = new ReplaySubject<MetaInfoResponse | null>(1);
    this.refreshUser();

    if (followUp) {
      console.log('Navigating to follow up:', followUp);
      this.whenKnown().subscribe(() => this.router.navigate([followUp]));
    }
  }

  logout(): void {
    this.http.post('/api/Identity/Logout', null).subscribe({
      error: () => this.notifier.open("Logout failed", "Close"),
      next: () => {
        this.session.signedInOrOut();
        this.setUser(null);
        // Home, signed out (not left on a page that was theirs)
        this.router.navigate(['/']);
      },
    });
  }

  private setUser(user: MetaInfoResponse | null): void {
    this.user.set(user);
    this.known.next(user);
  }

  generatePasswordResetLink(request: GeneratePasswordResetLinkRequestDto): Observable<GeneratePasswordResetLinkResponseDto> {
    return this.http.post<GeneratePasswordResetLinkResponseDto>('/api/admin/generatepasswordresetlink', request);
  }

  resetPassword(request: UsePasswordResetLinkRequestDto): Observable<string> {
    return this.http.post('/api/identity/resetpassword', request, { responseType: 'text' });
  }

  cleanupExpiredPasswordResetLinks(): Observable<string> {
    return this.http.post('/api/admin/cleanupexpiredpasswordresetlinks', {}, { responseType: 'text' });
  }
}

/** Drops the tokens and user earlier versions kept in localStorage, where any script could read them. */
function clearLegacyStorage(): void {
  try {
    for (const key of LEGACY_STORAGE_KEYS)
      localStorage.removeItem(key);
  } catch {
    // Storage blocked: nothing was kept there either
  }
}
