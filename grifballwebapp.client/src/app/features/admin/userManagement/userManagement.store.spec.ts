import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '@core/auth/account.service';
import type { GeneratePasswordResetLinkResponseDto } from '@api/models';
import { of, throwError } from 'rxjs';
import { UserManagementStore } from './userManagement.store';

describe('UserManagementStore', () => {
  let store: UserManagementStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let account: jasmine.SpyObj<AccountService>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    account = jasmine.createSpyObj<AccountService>('AccountService', ['generatePasswordResetLink']);
    TestBed.configureTestingModule({
      providers: [
        UserManagementStore, provideHttpClient(withXhr()), provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AccountService, useValue: account },
      ],
    });
    store = TestBed.inject(UserManagementStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("asks for a page of users as the table asks, with its search", () => {
    store.request({ pageNumber: 2, pageSize: 10, sortColumn: 'userName', sortDirection: 'asc', filters: [{ column: 'search', value: 'grif' }] }).subscribe();
    const page = http.expectOne(r => r.url === '/api/users');
    expect(page.request.params.get('pageNumber')).toBe('2');
    expect(page.request.params.get('pageSize')).toBe('10');
    expect(page.request.params.get('sortColumn')).toBe('userName');
    expect(page.request.params.get('search')).toBe('grif');
    page.flush({ totalCount: 0, results: [] });
  });

  it("makes a user's password reset link, and says when it cannot", () => {
    const made = jasmine.createSpy('made');
    const response = { resetLink: '/reset?token=x', expiresAt: '2026-11-15T00:00:00Z' } as GeneratePasswordResetLinkResponseDto;
    account.generatePasswordResetLink.and.returnValue(of(response));
    store.generatePasswordResetLink('grif', made);
    expect(account.generatePasswordResetLink).toHaveBeenCalledWith({ username: 'grif' });
    expect(made).toHaveBeenCalledWith(response);

    account.generatePasswordResetLink.and.returnValue(throwError(() => new Error('down')));
    store.generatePasswordResetLink('grif', made);
    expect(made).toHaveBeenCalledTimes(1);
    expect(snackBar.open).toHaveBeenCalledWith('Failed to generate password reset link', 'Close', { duration: 5000 });
  });
});
