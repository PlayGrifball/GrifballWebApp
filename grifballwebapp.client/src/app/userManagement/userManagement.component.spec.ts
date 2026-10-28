import { TableComponent } from '../shared/table/table.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { DateTime } from 'luxon';
import { DatePipe } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserManagementComponent } from './userManagement.component';
import { AccountService } from '../account.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { UserResponseDto } from './userResponseDto';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideRouter } from '@angular/router';

describe('UserManagementComponent', () => {
  let component: UserManagementComponent;
  let fixture: ComponentFixture<UserManagementComponent>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;

  // Helper function to mock clipboard
  function mockClipboard(shouldSucceed: boolean) {
    const mockClipboard = {
      writeText: jasmine.createSpy('writeText').and.returnValue(
        shouldSucceed ? Promise.resolve() : Promise.reject(new Error('Clipboard error'))
      )
    };
    Object.defineProperty(navigator, 'clipboard', {
      value: mockClipboard,
      writable: true,
      configurable: true
    });
    return mockClipboard;
  }

  beforeEach(async () => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['generatePasswordResetLink']);
    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open']);

    await TestBed.configureTestingModule({
      imports: [UserManagementComponent, NoopAnimationsModule],
      providers: [
        { provide: AccountService, useValue: mockAccountService },
        { provide: MatSnackBar, useValue: mockSnackBar },
        provideHttpClient(withXhr()),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserManagementComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have correct displayed columns', () => {
    expect(component.displayedColumns).toContain('userID');
    expect(component.displayedColumns).toContain('userName');
    expect(component.displayedColumns).toContain('externalAuthCount');
    expect(component.displayedColumns).toContain('actions');
  });

  describe('generatePasswordResetLink', () => {
    it('should call generatePasswordResetLink for any user', () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'oauthuser',
        externalAuthCount: 1,
        hasPassword: false,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'OAuth User',
        gamertag: 'OAuthGT',
        discord: 'discord#1234',
        roles: []
      };

      const mockResponse = {
        resetLink: '/reset-password?token=abc123',
        expiresAt: '2025-01-01T12:00:00Z'
      };

      mockAccountService.generatePasswordResetLink.and.returnValue(of(mockResponse));

      component.generatePasswordResetLink(user);

      expect(mockAccountService.generatePasswordResetLink).toHaveBeenCalledWith({
        username: 'oauthuser'
      });
    });

    it('should call generatePasswordResetLink for users with internal login', () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'internaluser',
        externalAuthCount: 0,
        hasPassword: true,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'Internal User',
        gamertag: 'InternalGT',
        discord: null,
        roles: []
      };

      const mockResponse = {
        resetLink: '/reset-password?token=abc123',
        expiresAt: '2025-01-01T12:00:00Z'
      };

      mockAccountService.generatePasswordResetLink.and.returnValue(of(mockResponse));

      component.generatePasswordResetLink(user);

      expect(mockAccountService.generatePasswordResetLink).toHaveBeenCalledWith({
        username: 'internaluser'
      });
    });

    it('should copy full URL to clipboard on success', async () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'testuser',
        externalAuthCount: 0,
        hasPassword: true,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'Test User',
        gamertag: 'TestGT',
        discord: null,
        roles: []
      };

      const mockResponse = {
        resetLink: '/reset-password?token=abc123',
        expiresAt: '2025-01-01T12:00:00Z'
      };

      const clipboard = mockClipboard(true);
      mockAccountService.generatePasswordResetLink.and.returnValue(of(mockResponse));

      component.generatePasswordResetLink(user);

      // Wait for the async clipboard operation
      await new Promise(resolve => setTimeout(resolve, 10));

      expect(clipboard.writeText).toHaveBeenCalled();
      const clipboardCall = (clipboard.writeText as jasmine.Spy).calls.mostRecent();
      expect(clipboardCall.args[0]).toContain('/reset-password?token=abc123');
    });

    it('should show success message with expiration time after copying to clipboard', async () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'testuser',
        externalAuthCount: 0,
        hasPassword: true,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'Test User',
        gamertag: 'TestGT',
        discord: null,
        roles: []
      };

      const mockResponse = {
        resetLink: '/reset-password?token=abc123',
        expiresAt: '2025-01-01T12:00:00Z'
      };

      mockClipboard(true);
      mockAccountService.generatePasswordResetLink.and.returnValue(of(mockResponse));

      component.generatePasswordResetLink(user);

      // Wait for the async clipboard operation
      await new Promise(resolve => setTimeout(resolve, 10));

      expect(mockSnackBar.open).toHaveBeenCalled();
      const snackBarCall = mockSnackBar.open.calls.mostRecent();
      expect(snackBarCall.args[0]).toContain('Password reset link copied to clipboard!');
      expect(snackBarCall.args[2]).toEqual({ duration: 10000 });
    });

    it('should show fallback message when clipboard copy fails', async () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'testuser',
        externalAuthCount: 0,
        hasPassword: true,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'Test User',
        gamertag: 'TestGT',
        discord: null,
        roles: []
      };

      const mockResponse = {
        resetLink: '/reset-password?token=abc123',
        expiresAt: '2025-01-01T12:00:00Z'
      };

      mockClipboard(false);
      mockAccountService.generatePasswordResetLink.and.returnValue(of(mockResponse));

      component.generatePasswordResetLink(user);

      // Wait for the async clipboard operation to fail
      await new Promise(resolve => setTimeout(resolve, 10));

      expect(mockSnackBar.open).toHaveBeenCalled();
      const snackBarCall = mockSnackBar.open.calls.mostRecent();
      expect(snackBarCall.args[0]).toContain('Password reset link:');
      expect(snackBarCall.args[2]).toEqual({ duration: 15000 });
    });

    it('should show error message when API call fails', () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'testuser',
        externalAuthCount: 0,
        hasPassword: true,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'Test User',
        gamertag: 'TestGT',
        discord: null,
        roles: []
      };

      mockAccountService.generatePasswordResetLink.and.returnValue(
        throwError(() => ({ error: 'User not found' }))
      );

      component.generatePasswordResetLink(user);

      expect(mockSnackBar.open).toHaveBeenCalledWith(
        'User not found',
        'Close',
        { duration: 5000 }
      );
    });

    it('should show generic error message when error is not a string', () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'testuser',
        externalAuthCount: 0,
        hasPassword: true,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'Test User',
        gamertag: 'TestGT',
        discord: null,
        roles: []
      };

      mockAccountService.generatePasswordResetLink.and.returnValue(
        throwError(() => ({ error: { message: 'Object error' } }))
      );

      component.generatePasswordResetLink(user);

      expect(mockSnackBar.open).toHaveBeenCalledWith(
        'Failed to generate password reset link',
        'Close',
        { duration: 5000 }
      );
    });

    it('should allow password reset/setup for users with multiple external auth', () => {
      const user: UserResponseDto = {
        userID: 1,
        userName: 'multiauth',
        externalAuthCount: 3,
        hasPassword: false,
        lockoutEnd: null,
        lockoutEnabled: false,
        isDummyUser: false,
        accessFailedCount: 0,
        region: 'US',
        displayName: 'Multi Auth',
        gamertag: 'MultiGT',
        discord: 'discord#9999',
        roles: []
      };

      const mockResponse = {
        resetLink: '/reset-password?token=xyz789',
        expiresAt: '2025-01-01T12:00:00Z'
      };

      mockAccountService.generatePasswordResetLink.and.returnValue(of(mockResponse));

      component.generatePasswordResetLink(user);

      expect(mockAccountService.generatePasswordResetLink).toHaveBeenCalledWith({
        username: 'multiauth'
      });
    });
  });

  describe('filter signal', () => {
    it('should initialize with empty string', () => {
      expect(component.filter()).toBe('');
    });

    it('should update filters computed signal when filter changes', () => {
      component.filter.set('test');
      
      const filters = component.filters();
      expect(filters).toEqual([
        { column: 'search', value: 'test' }
      ]);
    });

    it('should update filters computed signal when filter is cleared', () => {
      component.filter.set('test');
      component.filter.set('');
      
      const filters = component.filters();
      expect(filters).toEqual([
        { column: 'search', value: '' }
      ]);
    });
  });
});

describe('UserManagementComponent user table', () => {
  let fixture: ComponentFixture<UserManagementComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserManagementComponent, NoopAnimationsModule],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: AccountService, useValue: {} }]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(UserManagementComponent);
  });

  it('shows lockoutEnd as the date pipe showed the API value, from a real DateTime', async () => {
    // The page request is a pending task, so render without waiting for stability first.
    fixture.detectChanges();
    // GrifballWebApp.Server UserResponseDto as the API sends it: lockoutEnd is a DateTimeOffset.
    http.expectOne(r => r.url.startsWith('/api/UserManagement/GetUsers')).flush({
      totalCount: 1,
      results: [{
        userID: 1, userName: 'grif', lockoutEnd: '2026-03-04T05:06:00+00:00', lockoutEnabled: true, isDummyUser: false,
        accessFailedCount: 0, region: null, displayName: 'Grif', gamertag: 'SgtGrif', discord: null,
        externalAuthCount: 0, hasPassword: true, roles: [],
      }],
    });
    await fixture.whenStable();

    const table = fixture.debugElement.query(By.directive(TableComponent)).componentInstance as TableComponent<UserResponseDto>;
    expect(table.current().results[0].lockoutEnd instanceof DateTime).toBeTrue();
    const cells = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row td')).map(td => td.textContent!.trim());
    expect(cells).toContain(new DatePipe('en-US').transform('2026-03-04T05:06:00Z', 'short')!);
  });
});
