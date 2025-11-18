import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { isEventOrganizerGuard } from './isEventOrganizer.guard';
import { AccountService } from './account.service';

describe('isEventOrganizerGuard', () => {
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockRoute: ActivatedRouteSnapshot;
  let mockState: RouterStateSnapshot;

  beforeEach(() => {
    mockAccountService = jasmine.createSpyObj('AccountService', [], {
      isEventOrganizer: jasmine.createSpy('isEventOrganizer')
    });

    TestBed.configureTestingModule({
      providers: [
        { provide: AccountService, useValue: mockAccountService }
      ]
    });

    mockRoute = {} as ActivatedRouteSnapshot;
    mockState = {} as RouterStateSnapshot;
  });

  it('should allow access when user is event organizer', () => {
    (mockAccountService.isEventOrganizer as jasmine.Spy).and.returnValue(true);

    const result = TestBed.runInInjectionContext(() =>
      isEventOrganizerGuard(mockRoute, mockState)
    );

    expect(result).toBe(true);
  });

  it('should deny access when user is not event organizer', () => {
    (mockAccountService.isEventOrganizer as jasmine.Spy).and.returnValue(false);

    const result = TestBed.runInInjectionContext(() =>
      isEventOrganizerGuard(mockRoute, mockState)
    );

    expect(result).toBe(false);
  });
});
