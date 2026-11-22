import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { isStaffGuard } from './isStaff.guard';
import { AccountService } from './account.service';
import { MetaInfoResponse } from './accessTokenResponse';

describe('isStaffGuard', () => {
  let known: Subject<MetaInfoResponse | null>;
  let commissioner: boolean;
  let sysAdmin: boolean;

  beforeEach(() => {
    known = new Subject();
    commissioner = false;
    sysAdmin = false;
    TestBed.configureTestingModule({
      providers: [
        { provide: AccountService, useValue: { whenKnown: () => known, isEventOrganizer: () => commissioner, isSysAdmin: () => sysAdmin } },
      ],
    });
  });

  const run = () => TestBed.runInInjectionContext(() =>
    isStaffGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as Observable<boolean>;

  it('allows a commissioner once the user is known', async () => {
    const result = firstValueFrom(run());
    commissioner = true;
    known.next({ isEventOrganizer: true } as MetaInfoResponse);
    expect(await result).toBe(true);
  });

  it('allows a sysadmin once the user is known', async () => {
    const result = firstValueFrom(run());
    sysAdmin = true;
    known.next({ isSysAdmin: true } as MetaInfoResponse);
    expect(await result).toBe(true);
  });

  it('refuses anyone else', async () => {
    const result = firstValueFrom(run());
    known.next(null);
    expect(await result).toBe(false);
  });

  it('does not decide before the API has said who is signed in', () => {
    let decided = false;
    run().subscribe(() => decided = true);
    expect(decided).toBe(false);
    known.next(null);
    expect(decided).toBe(true);
  });
});
