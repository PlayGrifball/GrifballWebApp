import { PLATFORM_ID, TransferState } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Notifier } from './shared/notifier.service';
import { AccountService, LEGACY_STORAGE_KEYS, ME_URL, USER_STATE_KEY } from './account.service';
import { MetaInfoResponse } from './accessTokenResponse';
import { ANTIFORGERY_URL, AuthSession } from './authSession.service';
import { authInterceptor } from './auth.interceptor';

const commish: MetaInfoResponse = { isSysAdmin: true, isCommissioner: true, isPlayer: false, userID: 42, displayName: 'Commish' };
const player: MetaInfoResponse = { isSysAdmin: false, isCommissioner: false, isPlayer: true, userID: 7, displayName: 'Chief' };

describe('AccountService', () => {
  let http: HttpTestingController;
  let notifier: jasmine.SpyObj<Notifier>;
  let router: jasmine.SpyObj<Router>;

  function configure(platform = 'browser'): void {
    notifier = jasmine.createSpyObj('Notifier', ['open']);
    router = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: PLATFORM_ID, useValue: platform },
        { provide: Notifier, useValue: notifier },
        { provide: Router, useValue: router },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  }

  /** The service, after the API has said who is signed in. */
  function signedInAs(user: MetaInfoResponse | null): AccountService {
    const service = TestBed.inject(AccountService);
    const me = http.expectOne(ME_URL);
    me.flush(user, user === null ? { status: 204, statusText: 'No Content' } : undefined);
    return service;
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  describe('who is signed in', () => {
    beforeEach(() => configure());

    it('is not known until the API has said', () => {
      const service = TestBed.inject(AccountService);
      const known: (MetaInfoResponse | null)[] = [];
      service.whenKnown().subscribe(user => known.push(user));

      expect(service.user()).toBeUndefined();
      expect(service.isLoggedIn()).toBeFalse();
      expect(known).toEqual([]);

      const me = http.expectOne(ME_URL);
      expect(me.request.method).toBe('GET');
      expect(me.request.headers.has('Authorization')).toBeFalse();
      // The user goes with the page under its own key, not in the HTTP transfer cache
      expect(me.request.transferCache).toBeFalse();
      me.flush(commish);
      expect(known).toEqual([commish]);
    });

    it('takes the roles, id and name from the API', () => {
      const service = signedInAs(commish);

      expect(service.isLoggedIn()).toBeTrue();
      expect(service.isSysAdmin()).toBeTrue();
      expect(service.isEventOrganizer()).toBeTrue();
      expect(service.isPlayer()).toBeFalse();
      expect(service.personID()).toBe(42);
      expect(service.displayName()).toBe('Commish');
    });

    it('is a visitor when the API answers 204 No Content', async () => {
      const service = signedInAs(null);

      expect(service.isLoggedIn()).toBeFalse();
      expect(service.isSysAdmin()).toBeFalse();
      expect(service.isEventOrganizer()).toBeFalse();
      expect(service.isPlayer()).toBeFalse();
      expect(service.personID()).toBeNull();
      expect(service.displayName()).toBeNull();
      expect(await firstValueFrom(service.whenKnown())).toBeNull();
    });

    it('is a visitor when the API cannot be reached', () => {
      const service = TestBed.inject(AccountService);
      http.expectOne(ME_URL).flush('down', { status: 502, statusText: 'Bad Gateway' });

      expect(service.user()).toBeNull();
      expect(service.isLoggedIn()).toBeFalse();
    });
  });

  describe('tokens from earlier versions', () => {
    it('removes the access token and user kept in localStorage, and does not use them', () => {
      localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'old', refreshToken: 'old-refresh', expiresIn: 3600 }));
      localStorage.setItem('metaInfo', JSON.stringify(commish));
      localStorage.setItem('theme', 'light-theme');
      configure();

      const service = TestBed.inject(AccountService);

      for (const key of LEGACY_STORAGE_KEYS)
        expect(localStorage.getItem(key)).withContext(key).toBeNull();
      // Anything else stays
      expect(localStorage.getItem('theme')).toBe('light-theme');
      // Not signed in from what was stored: only the API says
      expect(service.isLoggedIn()).toBeFalse();
      http.expectOne(ME_URL).flush(null, { status: 204, statusText: 'No Content' });
      expect(service.isLoggedIn()).toBeFalse();
    });

    it('carries on when storage is blocked', () => {
      spyOn(localStorage, 'removeItem').and.throwError('SecurityError');
      configure();

      expect(() => TestBed.inject(AccountService)).not.toThrow();
      http.expectOne(ME_URL).flush(null, { status: 204, statusText: 'No Content' });
    });
  });

  describe('signing in and out', () => {
    beforeEach(() => configure());

    it('signs in with a password (the API sets the cookie), then asks who it is', () => {
      const service = signedInAs(null);
      const changed = jasmine.createSpy('changed');
      TestBed.inject(AuthSession).changed$.subscribe(changed);

      service.login({ username: 'chief', password: 'pw' });
      http.expectOne(ANTIFORGERY_URL).flush({ token: 'visitor-token' });
      const login = http.expectOne('/api/identity/login');
      expect(login.request.method).toBe('POST');
      expect(login.request.body).toEqual({ username: 'chief', password: 'pw' });
      login.flush('');

      expect(changed).toHaveBeenCalledTimes(1);
      expect(service.user()).toBeUndefined();
      http.expectOne(ME_URL).flush(player);
      expect(service.displayName()).toBe('Chief');
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('fetches a new antiforgery token after signing in', () => {
      const service = signedInAs(null);
      service.login({ username: 'chief', password: 'pw' });
      http.expectOne(ANTIFORGERY_URL).flush({ token: 'visitor-token' });
      http.expectOne('/api/identity/login').flush('');
      http.expectOne(ME_URL).flush(player);

      service.cleanupExpiredPasswordResetLinks().subscribe();

      http.expectOne(ANTIFORGERY_URL).flush({ token: 'chief-token' });
      expect(http.expectOne('/api/admin/cleanupexpiredpasswordresetlinks').request.headers.get('X-XSRF-TOKEN')).toBe('chief-token');
    });

    it('says so when the password is wrong', () => {
      const service = signedInAs(null);

      service.login({ username: 'chief', password: 'wrong' });
      http.expectOne(ANTIFORGERY_URL).flush({ token: 't' });
      http.expectOne('/api/identity/login').flush('', { status: 401, statusText: 'Unauthorized' });

      expect(notifier.open).toHaveBeenCalledWith('Login failed', 'Close');
      expect(service.isLoggedIn()).toBeFalse();
      http.expectNone(ME_URL);
    });

    it('finishes a Discord sign-in, then goes to the follow-up page once it knows the user', () => {
      const service = signedInAs(null);
      spyOn(console, 'log');

      service.loginExternal('/season/12/teams');
      const callback = http.expectOne('/api/Identity/ExternalLoginCallback');
      expect(callback.request.method).toBe('GET');
      callback.flush('Signed In');
      expect(router.navigate).not.toHaveBeenCalled();

      http.expectOne(ME_URL).flush(player);
      expect(service.isPlayer()).toBeTrue();
      expect(router.navigate).toHaveBeenCalledOnceWith(['/season/12/teams']);
    });

    it('does not navigate after a Discord sign-in without a follow-up page', () => {
      const service = signedInAs(null);

      service.loginExternal('');
      http.expectOne('/api/Identity/ExternalLoginCallback').flush('');
      http.expectOne(ME_URL).flush(player);

      expect(service.isLoggedIn()).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('says so when the Discord sign-in fails', () => {
      const service = signedInAs(null);

      service.loginExternal('/somewhere');
      http.expectOne('/api/Identity/ExternalLoginCallback').flush('no', { status: 400, statusText: 'Bad Request' });

      expect(notifier.open).toHaveBeenCalledWith('Login failed', 'Close');
      expect(service.isLoggedIn()).toBeFalse();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('signs out on the API (which clears the cookie), then is a visitor', () => {
      const service = signedInAs(commish);
      const changed = jasmine.createSpy('changed');
      TestBed.inject(AuthSession).changed$.subscribe(changed);

      service.logout();
      http.expectOne(ANTIFORGERY_URL).flush({ token: 't' });
      const logout = http.expectOne('/api/Identity/Logout');
      expect(logout.request.method).toBe('POST');
      expect(service.isLoggedIn()).toBeTrue();
      logout.flush(null, { status: 204, statusText: 'No Content' });

      expect(changed).toHaveBeenCalledTimes(1);
      expect(service.isLoggedIn()).toBeFalse();
      expect(service.isSysAdmin()).toBeFalse();
      expect(service.displayName()).toBeNull();
      http.expectNone(ME_URL);
    });

    it('stays signed in, and says so, when signing out fails', () => {
      const service = signedInAs(commish);

      service.logout();
      http.expectOne(ANTIFORGERY_URL).flush({ token: 't' });
      http.expectOne('/api/Identity/Logout').flush('down', { status: 502, statusText: 'Bad Gateway' });

      expect(notifier.open).toHaveBeenCalledWith('Logout failed', 'Close');
      expect(service.isLoggedIn()).toBeTrue();
    });

    it('registers', () => {
      const service = signedInAs(null);
      service.register({ username: 'new', password: 'pw' } as never).subscribe();

      http.expectOne(ANTIFORGERY_URL).flush({ token: 't' });
      expect(http.expectOne('/api/identity/register').request.method).toBe('POST');
    });
  });

  describe('password reset', () => {
    let service: AccountService;
    beforeEach(() => {
      configure();
      service = signedInAs(commish);
    });

    it('generates a reset link for a user', () => {
      const request = { username: 'bob' };
      let response: any;
      service.generatePasswordResetLink(request).subscribe(r => response = r);

      http.expectOne(ANTIFORGERY_URL).flush({ token: 't' });
      const req = http.expectOne('/api/admin/generatepasswordresetlink');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toBe(request);
      req.flush({ resetLink: 'https://example/reset?t=abc', expiresAt: '2026-01-01T00:00:00Z' });
      expect(response.resetLink).toBe('https://example/reset?t=abc');
    });

    it('resets a password and returns the plain-text message', () => {
      const request = { token: 't', newPassword: 'p' };
      let response: string | undefined;
      service.resetPassword(request).subscribe(r => response = r);

      http.expectOne(ANTIFORGERY_URL).flush({ token: 't' });
      const req = http.expectOne('/api/identity/resetpassword');
      expect(req.request.method).toBe('POST');
      expect(req.request.responseType).toBe('text');
      req.flush('Password reset');
      expect(response).toBe('Password reset');
    });

    it('cleans up expired reset links', () => {
      let response: string | undefined;
      service.cleanupExpiredPasswordResetLinks().subscribe(r => response = r);

      http.expectOne(ANTIFORGERY_URL).flush({ token: 't' });
      const req = http.expectOne('/api/admin/cleanupexpiredpasswordresetlinks');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      expect(req.request.responseType).toBe('text');
      req.flush('Removed 3');
      expect(response).toBe('Removed 3');
    });
  });

  describe('on the server', () => {
    beforeEach(() => configure('server'));

    it('asks the API with the page\'s cookie, and sends the answer with the page', () => {
      const service = signedInAs(player);

      expect(service.displayName()).toBe('Chief');
      expect(TestBed.inject(TransferState).get(USER_STATE_KEY, undefined)).toEqual(player);
    });

    it('sends a visitor along as null', () => {
      signedInAs(null);

      expect(TestBed.inject(TransferState).hasKey(USER_STATE_KEY)).toBeTrue();
      expect(TestBed.inject(TransferState).get(USER_STATE_KEY, undefined)).toBeNull();
    });

    it('renders as a visitor when the API does not answer, and sends nothing along (the browser asks itself)', () => {
      const service = TestBed.inject(AccountService);
      http.expectOne(ME_URL).flush('timeout', { status: 504, statusText: 'Gateway Timeout' });

      expect(service.isLoggedIn()).toBeFalse();
      expect(TestBed.inject(TransferState).hasKey(USER_STATE_KEY)).toBeFalse();
    });

    it('leaves the browser\'s storage alone', () => {
      localStorage.setItem('access_token', 'kept');
      signedInAs(null);
      expect(localStorage.getItem('access_token')).toBe('kept');
    });
  });

  describe('in the browser, on a page the server rendered', () => {
    beforeEach(() => configure());

    it('starts as the user the server rendered, without asking the API again', () => {
      TestBed.inject(TransferState).set(USER_STATE_KEY, commish);

      const service = TestBed.inject(AccountService);

      expect(service.displayName()).toBe('Commish');
      expect(service.isEventOrganizer()).toBeTrue();
      http.expectNone(ME_URL);
      // Used once: the next sign-in asks the API
      expect(TestBed.inject(TransferState).hasKey(USER_STATE_KEY)).toBeFalse();
    });

    it('starts as a visitor when the server rendered one', async () => {
      TestBed.inject(TransferState).set(USER_STATE_KEY, null);

      const service = TestBed.inject(AccountService);

      expect(service.user()).toBeNull();
      expect(await firstValueFrom(service.whenKnown())).toBeNull();
      http.expectNone(ME_URL);
    });
  });
});
