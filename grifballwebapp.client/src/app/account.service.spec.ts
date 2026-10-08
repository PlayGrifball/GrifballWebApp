import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { JwtHelperService } from '@auth0/angular-jwt';

import { AccountService } from './account.service';
import { ApiClientService } from './api/apiClient.service';
import { AccessTokenResponse, MetaInfoResponse } from './accessTokenResponse';
import { LoginDto } from './api/dtos/loginDto';
import { RegisterDto } from './api/dtos/registerDto';

describe('AccountService', () => {
  let service: AccountService;
  let httpTestingController: HttpTestingController;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockJwtHelper: jasmine.SpyObj<JwtHelperService>;
  let mockApiClient: jasmine.SpyObj<ApiClientService>;

  beforeEach(() => {
    // Clear localStorage before each test
    localStorage.clear();

    // Create spies
    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockJwtHelper = jasmine.createSpyObj('JwtHelperService', ['isTokenExpired', 'decodeToken']);
    mockApiClient = jasmine.createSpyObj('ApiClientService', ['get', 'post']);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AccountService,
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: Router, useValue: mockRouter },
        { provide: JwtHelperService, useValue: mockJwtHelper },
        { provide: ApiClientService, useValue: mockApiClient }
      ]
    });

    service = TestBed.inject(AccountService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // Verify that no unmatched requests are outstanding
    httpTestingController.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should initialize with no user logged in', () => {
    expect(service.isLoggedIn()).toBeFalsy();
    expect(service.accessToken()).toBeUndefined();
    expect(service.isSysAdmin()).toBeFalsy();
    expect(service.isEventOrganizer()).toBeFalsy();
    expect(service.isPlayer()).toBeFalsy();
    expect(service.personID()).toBeNull();
    expect(service.displayName()).toBeNull();
  });

  it('should load access token from localStorage on initialization', () => {
    // Setup localStorage with a mock token BEFORE creating service
    const mockAccessToken: AccessTokenResponse = {
      tokenType: 'Bearer',
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600
    };
    localStorage.setItem('access_token', JSON.stringify(mockAccessToken));

    // Create a fresh TestBed with new service instance
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AccountService,
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: Router, useValue: mockRouter },
        { provide: JwtHelperService, useValue: mockJwtHelper },
        { provide: ApiClientService, useValue: mockApiClient }
      ]
    });
    
    const newService = TestBed.inject(AccountService);
    
    expect(newService.accessToken()).toBe('mock-access-token');
    expect(newService.isLoggedIn()).toBeTruthy();
  });

  it('should load meta info from localStorage on initialization', () => {
    const mockMetaInfo: MetaInfoResponse = {
      isSysAdmin: true,
      isCommissioner: false,
      isPlayer: true,
      userID: 123,
      displayName: 'Test User'
    };
    localStorage.setItem('metaInfo', JSON.stringify(mockMetaInfo));

    // Create a fresh TestBed with new service instance
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AccountService,
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: Router, useValue: mockRouter },
        { provide: JwtHelperService, useValue: mockJwtHelper },
        { provide: ApiClientService, useValue: mockApiClient }
      ]
    });

    const newService = TestBed.inject(AccountService);

    expect(newService.isSysAdmin()).toBeTruthy();
    expect(newService.isEventOrganizer()).toBeFalsy(); // isCommissioner is false in mockMetaInfo
    expect(newService.isPlayer()).toBeTruthy();
    expect(newService.personID()).toBe(123);
    expect(newService.displayName()).toBe('Test User');
  });

  it('should register a user', () => {
    const registerDto: RegisterDto = {
      username: 'testuser',
      password: 'password123'
    };

    service.register(registerDto).subscribe();

    const req = httpTestingController.expectOne('/api/identity/register');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(registerDto);

    req.flush({});
  });

  it('should handle successful login', () => {
    const loginDto: LoginDto = {
      username: 'testuser',
      password: 'password123'
    };

    const mockResponse: AccessTokenResponse = {
      tokenType: 'Bearer',
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600
    };

    service.login(loginDto);

    const req = httpTestingController.expectOne('/api/identity/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(loginDto);

    req.flush(mockResponse);

    expect(service.accessToken()).toBe('mock-access-token');
    expect(service.isLoggedIn()).toBeTruthy();
    expect(localStorage.getItem('access_token')).toBe(JSON.stringify(mockResponse));
  });

  it('should handle failed login', () => {
    const loginDto: LoginDto = {
      username: 'testuser',
      password: 'wrongpassword'
    };

    service.login(loginDto);

    const req = httpTestingController.expectOne('/api/identity/login');
    req.flush('Login failed', { status: 401, statusText: 'Unauthorized' });

    expect(mockSnackBar.open).toHaveBeenCalledWith('Login failed', 'Close');
    expect(service.isLoggedIn()).toBeFalsy();
  });

  it('should logout user', () => {
    // First set up a logged in state
    const mockAccessToken: AccessTokenResponse = {
      tokenType: 'Bearer',
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600
    };
    service.accessTokenResponse.set(mockAccessToken);

    expect(service.isLoggedIn()).toBeTruthy();

    // Now logout
    service.logout();

    expect(service.isLoggedIn()).toBeFalsy();
    expect(service.accessToken()).toBeUndefined();
    expect(localStorage.getItem('access_token')).toBeNull();
  });

  it('should handle external login callback', () => {
    const mockResponse: AccessTokenResponse = {
      tokenType: 'Bearer',
      accessToken: 'external-token',
      refreshToken: 'external-refresh',
      expiresIn: 3600
    };

    const followUpUrl = '/dashboard';
    service.loginExternal(followUpUrl);

    const req = httpTestingController.expectOne('/api/Identity/ExternalLoginCallback');
    expect(req.request.method).toBe('GET');

    req.flush(mockResponse);

    expect(service.accessToken()).toBe('external-token');
    expect(service.isLoggedIn()).toBeTruthy();
    expect(mockRouter.navigate).toHaveBeenCalledWith([followUpUrl]);
  });
});
describe('AccountService session handling', () => {
  let service: AccountService;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let router: jasmine.SpyObj<Router>;

  const token = (overrides: Partial<AccessTokenResponse> = {}): AccessTokenResponse => ({
    tokenType: 'Bearer',
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    expiresIn: 3600,
    ...overrides
  });

  const meta: MetaInfoResponse = {
    isSysAdmin: true,
    isCommissioner: true,
    isPlayer: false,
    userID: 42,
    displayName: 'Commish'
  };

  function create(): void {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    router = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AccountService,
        { provide: MatSnackBar, useValue: snackBar },
        { provide: Router, useValue: router },
        { provide: JwtHelperService, useValue: {} },
        { provide: ApiClientService, useValue: {} }
      ]
    });
    service = TestBed.inject(AccountService);
    http = TestBed.inject(HttpTestingController);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  describe('role lookup', () => {
    it('fetches the roles once logged in and caches them in localStorage', () => {
      localStorage.setItem('access_token', JSON.stringify(token()));
      create();
      TestBed.tick();

      http.expectOne('/api/identity/metaInfo').flush(meta);

      expect(service.isSysAdmin()).toBeTrue();
      expect(service.isEventOrganizer()).toBeTrue();
      expect(service.isPlayer()).toBeFalse();
      expect(service.displayName()).toBe('Commish');
      expect(JSON.parse(localStorage.getItem('metaInfo')!)).toEqual(meta);
    });

    it('clears every role when the role lookup fails', () => {
      localStorage.setItem('access_token', JSON.stringify(token()));
      localStorage.setItem('metaInfo', JSON.stringify({ ...meta, isPlayer: true }));
      spyOn(console, 'log');
      create();
      expect(service.isSysAdmin()).toBeTrue();
      TestBed.tick();

      http.expectOne('/api/identity/metaInfo').flush('no', { status: 500, statusText: 'Server Error' });

      expect(service.isSysAdmin()).toBeFalse();
      expect(service.isEventOrganizer()).toBeFalse();
      expect(service.isPlayer()).toBeFalse();
      expect(service.personID()).toBeNull();
      expect(service.displayName()).toBeNull();
    });

    it('clears every role on logout without calling the server', () => {
      localStorage.setItem('access_token', JSON.stringify(token()));
      create();
      TestBed.tick();
      http.expectOne('/api/identity/metaInfo').flush(meta);

      service.logout();
      TestBed.tick();

      http.expectNone('/api/identity/metaInfo');
      expect(service.isSysAdmin()).toBeFalse();
      expect(service.isEventOrganizer()).toBeFalse();
      expect(service.displayName()).toBeNull();
    });
  });

  describe('refresh', () => {
    it('returns null when there is no session to refresh', () => {
      create();
      expect(service.refresh()).toBeNull();
    });

    it('posts the refresh token and stores the new session', () => {
      create();
      service.accessTokenResponse.set(token());
      let result: AccessTokenResponse | undefined;

      service.refresh()!.subscribe(r => result = r);
      const req = http.expectOne('/api/Identity/Refresh');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ refreshToken: 'refresh-1' });
      req.flush(token({ accessToken: 'access-2', refreshToken: 'refresh-2' }));

      expect(result?.accessToken).toBe('access-2');
      expect(service.accessToken()).toBe('access-2');
      expect(JSON.parse(localStorage.getItem('access_token')!).refreshToken).toBe('refresh-2');
    });

    it('logs the user out and rethrows when the refresh is rejected', () => {
      create();
      spyOn(console, 'log');
      service.accessTokenResponse.set(token());
      localStorage.setItem('access_token', JSON.stringify(token()));
      let error: any;

      service.refresh()!.subscribe({ error: e => error = e });
      http.expectOne('/api/Identity/Refresh').flush('expired', { status: 401, statusText: 'Unauthorized' });

      expect(error.status).toBe(401);
      expect(service.isLoggedIn()).toBeFalse();
      expect(localStorage.getItem('access_token')).toBeNull();
    });
  });

  describe('external login', () => {
    it('does not navigate when there is no follow-up page', () => {
      create();
      service.loginExternal('');
      http.expectOne('/api/Identity/ExternalLoginCallback').flush(token());

      expect(service.isLoggedIn()).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('shows an error when the external login fails', () => {
      create();
      service.loginExternal('/somewhere');
      http.expectOne('/api/Identity/ExternalLoginCallback').flush('no', { status: 401, statusText: 'Unauthorized' });

      expect(snackBar.open).toHaveBeenCalledWith('Login failed', 'Close');
      expect(service.isLoggedIn()).toBeFalse();
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('password reset', () => {
    beforeEach(() => create());

    it('generates a reset link for a user', () => {
      const request = { username: 'bob' };
      let response: any;
      service.generatePasswordResetLink(request).subscribe(r => response = r);

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

      const req = http.expectOne('/api/identity/resetpassword');
      expect(req.request.method).toBe('POST');
      expect(req.request.responseType).toBe('text');
      req.flush('Password reset');
      expect(response).toBe('Password reset');
    });

    it('cleans up expired reset links', () => {
      let response: string | undefined;
      service.cleanupExpiredPasswordResetLinks().subscribe(r => response = r);

      const req = http.expectOne('/api/admin/cleanupexpiredpasswordresetlinks');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({});
      expect(req.request.responseType).toBe('text');
      req.flush('Removed 3');
      expect(response).toBe('Removed 3');
    });
  });
});
