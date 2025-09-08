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
    // Setup localStorage with a mock token
    const mockAccessToken: AccessTokenResponse = {
      tokenType: 'Bearer',
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600
    };
    localStorage.setItem('access_token', JSON.stringify(mockAccessToken));

    // Create a new service instance to test initialization
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

    // Create a new service instance to test initialization
    const newService = TestBed.inject(AccountService);

    expect(newService.isSysAdmin()).toBeTruthy();
    expect(newService.isEventOrganizer()).toBeFalsy();
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