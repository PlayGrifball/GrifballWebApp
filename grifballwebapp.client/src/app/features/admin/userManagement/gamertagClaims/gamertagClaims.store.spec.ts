import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { DuplicateSuggestionDto, GamertagClaimDto, GamertagClaimStatus } from '@api/models';
import { GamertagClaimsStore, pairKey } from './gamertagClaims.store';

const claim = (gamertagClaimID: number) => ({ gamertagClaimID, gamertag: 'Old Me', status: 'Pending' }) as GamertagClaimDto;
const pair = { sourceUserID: 5, targetUserID: 6, sourceGamertag: 'Grif1', targetGamertag: 'Grif' } as DuplicateSuggestionDto;
const problem = (detail: string, status: number) =>
  [{ title: 'Refused', detail }, { status, statusText: 'Refused', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('GamertagClaimsStore', () => {
  let store: GamertagClaimsStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let status: WritableSignal<GamertagClaimStatus>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [GamertagClaimsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(GamertagClaimsStore);
    http = TestBed.inject(HttpTestingController);
    status = signal<GamertagClaimStatus>('Pending');
    store.status.use(status);
    TestBed.tick();
    http.expectOne('/api/xuid-lookups/status').flush({ placeholders: 3 });
    claims('Pending').flush([claim(1)]);
    await settle();
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  function settle() {
    return TestBed.inject(ApplicationRef).whenStable();
  }
  function claims(wanted: string) {
    TestBed.tick();
    return http.expectOne(r => r.url === '/api/gamertag-claims' && r.params.get('status') === wanted);
  }

  it('loads the claims with the status chosen, and the lookup status', async () => {
    expect(store.rows()).toHaveSize(1);
    expect(store.xuidStatusValue()!.placeholders).toBe(3);

    status.set('Rejected');
    claims('Rejected').flush([]);
    await settle();
    expect(store.empty()).toBeTrue();

    status.set('Approved');
    claims('Approved').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
  });

  it('approves and rejects claims, with a reason, loading the list again', async () => {
    store.approve(claim(1));
    expect(store.acting()).toBe(1);
    http.expectOne({ method: 'POST', url: '/api/gamertag-claims/1/approve' }).flush(null);
    expect(store.acting()).toBeNull();
    expect(snackBar.open).toHaveBeenCalledWith('Approved the claim to Old Me.', 'Close');
    claims('Pending').flush([claim(2)]);
    await settle();

    store.reject(claim(2), 'Not you');
    const reject = http.expectOne({ method: 'POST', url: '/api/gamertag-claims/2/reject' });
    expect(reject.request.body).toEqual({ reason: 'Not you' });
    reject.flush(null);
    expect(snackBar.open).toHaveBeenCalledWith('Rejected the claim to Old Me.', 'Close');
    claims('Pending').flush([]);
    await settle();
  });

  it('says why a decision was refused, loading the list again when someone decided first', async () => {
    store.approve(claim(1));
    http.expectOne('/api/gamertag-claims/1/approve').flush(...problem('Not pending.', 400));
    expect(snackBar.open).toHaveBeenCalledWith('Not pending.', 'Close');
    http.expectNone(r => r.url === '/api/gamertag-claims');

    store.reject(claim(1), null);
    http.expectOne('/api/gamertag-claims/1/reject').flush(...problem('Decided already.', 409));
    claims('Pending').flush([]);
    await settle();
  });

  it('starts the lookup and reads its status again, and says when it cannot', () => {
    store.lookUpXuids();
    expect(store.lookingUp()).toBeTrue();
    http.expectOne({ method: 'POST', url: '/api/xuid-lookups/run' }).flush(null);
    expect(store.lookingUp()).toBeFalse();
    TestBed.tick();
    http.expectOne('/api/xuid-lookups/status').flush({ placeholders: 4 });

    store.lookUpXuids();
    http.expectOne('/api/xuid-lookups/run').flush(...problem('Running already.', 409));
    expect(store.lookingUp()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('Running already.', 'Close');
  });

  it('finds the possible duplicates on demand, again when asked again, and merges or dismisses a pair', async () => {
    store.findDuplicates();
    TestBed.tick();
    http.expectOne('/api/duplicate-suggestions').flush([pair]);
    await settle();
    expect(store.duplicateRows()).toHaveSize(1);
    expect(store.duplicatesLoaded()).toBeTrue();

    store.findDuplicates();
    TestBed.tick();
    expect(store.duplicatesLoading()).toBeTrue();
    http.expectOne('/api/duplicate-suggestions').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.duplicatesFailed()).toBeTrue();

    store.mergeDuplicate(pair);
    expect(store.actingPair()).toBe(pairKey(pair));
    const merge = http.expectOne({ method: 'POST', url: '/api/duplicate-suggestions/merge' });
    expect(merge.request.body).toEqual({ sourceUserID: 5, targetUserID: 6 });
    merge.flush(null);
    expect(store.actingPair()).toBeNull();
    expect(snackBar.open).toHaveBeenCalledWith('Merged Grif1 into Grif.', 'Close');
    TestBed.tick();
    http.expectOne('/api/duplicate-suggestions').flush([pair]);
    await settle();

    store.dismissDuplicate(pair);
    http.expectOne({ method: 'POST', url: '/api/duplicate-suggestions/dismiss' }).flush(...problem('Gone.', 404));
    expect(snackBar.open).toHaveBeenCalledWith('Gone.', 'Close');
    expect(store.actingPair()).toBeNull();
  });

  it('keys a pair by its two users', () => {
    expect(pairKey(pair)).toBe('5-6');
  });
});
