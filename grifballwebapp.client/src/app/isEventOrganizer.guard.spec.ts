import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { isEventOrganizerGuard } from './isEventOrganizer.guard';
import { AccountService } from './account.service';
import { MetaInfoResponse } from './accessTokenResponse';

describe('isEventOrganizerGuard', () => {
  let known: Subject<MetaInfoResponse | null>;
  let allowed: boolean;

  beforeEach(() => {
    known = new Subject();
    allowed = false;
    TestBed.configureTestingModule({
      providers: [
        { provide: AccountService, useValue: { whenKnown: () => known, isEventOrganizer: () => allowed } },
      ],
    });
  });

  const run = () => TestBed.runInInjectionContext(() =>
    isEventOrganizerGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as Observable<boolean>;

  it('allows a commissioner once the user is known', async () => {
    const result = firstValueFrom(run());
    allowed = true;
    known.next({ isCommissioner: true } as MetaInfoResponse);
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
