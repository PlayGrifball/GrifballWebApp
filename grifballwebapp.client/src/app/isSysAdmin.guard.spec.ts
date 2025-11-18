import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { isSysAdminGuard } from './isSysAdmin.guard';
import { AccountService } from './account.service';

describe('isSysAdminGuard', () => {
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockRoute: ActivatedRouteSnapshot;
  let mockState: RouterStateSnapshot;

  beforeEach(() => {
    mockAccountService = jasmine.createSpyObj('AccountService', [], {
      isSysAdmin: jasmine.createSpy('isSysAdmin')
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: AccountService, useValue: mockAccountService }
      ]
    });

    mockRoute = {} as ActivatedRouteSnapshot;
    mockState = {} as RouterStateSnapshot;
  });

  it('should allow access when user is sys admin', () => {
    (mockAccountService.isSysAdmin as jasmine.Spy).and.returnValue(true);

    const result = TestBed.runInInjectionContext(() =>
      isSysAdminGuard(mockRoute, mockState)
    );

    expect(result).toBe(true);
  });

  it('should deny access when user is not sys admin', () => {
    (mockAccountService.isSysAdmin as jasmine.Spy).and.returnValue(false);

    const result = TestBed.runInInjectionContext(() =>
      isSysAdminGuard(mockRoute, mockState)
    );

    expect(result).toBe(false);
  });
});
