import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PastGamertagsQuery, PastGamertagsStore } from './pastGamertags.store';

const account = { xboxUserID: 42, gamertag: 'Now', pastGamertags: [{ xboxGamertagHistoryID: 3, gamertag: 'Then' }] };
const refused = [{ title: 'Refused', detail: 'Not a manual row.' }, { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('PastGamertagsStore', () => {
  let store: PastGamertagsStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [PastGamertagsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(PastGamertagsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const lookup = () => {
    TestBed.tick();
    return http.expectOne(r => r.url === '/api/past-gamertags');
  };
  /** Looks up account 42 and answers it. */
  const show = async () => {
    store.query.use(signal<PastGamertagsQuery | undefined>({ xboxUserID: 42 }));
    lookup().flush(account);
    await settle();
  };

  it('looks nothing up without an account', () => {
    store.query.use(signal(undefined));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.account()).toBeNull();
  });

  it('looks the account up by XUID or user ID, and tells no such account from a failure', async () => {
    const query = signal<PastGamertagsQuery | undefined>({ xboxUserID: 42 });
    store.query.use(query);
    const byXuid = lookup();
    expect(byXuid.request.params.get('xboxUserID')).toBe('42');
    byXuid.flush(account);
    await settle();
    expect(store.rows()).toHaveSize(1);

    query.set({ userID: 7 });
    const byUser = lookup();
    expect(byUser.request.params.get('userID')).toBe('7');
    byUser.flush(null, { status: 404, statusText: 'Not Found' });
    await settle();
    expect(store.notFound()).toBeTrue();
    expect(store.rows()).toEqual([]);

    query.set({ userID: 8 });
    lookup().flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
  });

  it('removes a row and looks the account up again, and says why one could not be removed', async () => {
    await show();
    store.remove(3);
    expect(store.removing()).toBe(3);
    http.expectOne({ method: 'POST', url: '/api/past-gamertags/3/remove' }).flush(...refused);
    expect(store.removing()).toBeNull();
    expect(snackBar.open).toHaveBeenCalledWith('Not a manual row.', 'Close');

    store.remove(3);
    http.expectOne('/api/past-gamertags/3/remove').flush(null);
    lookup().flush(account);
    await settle();
  });

  it('adds a past gamertag and looks the account up again, and keeps why one could not be added', async () => {
    await show();
    const added = jasmine.createSpy('added');
    const request = { xboxUserID: 42, gamertag: 'Older', firstSeen: null, lastSeen: null, note: null };
    store.add(request, added);
    expect(store.adding()).toBeTrue();
    http.expectOne({ method: 'POST', url: '/api/past-gamertags' }).flush(...refused);
    expect(store.adding()).toBeFalse();
    expect(store.addError()).toBe('Not a manual row.');
    expect(added).not.toHaveBeenCalled();

    store.add(request, added);
    expect(store.addError()).toBeNull();
    const add = http.expectOne({ method: 'POST', url: '/api/past-gamertags' });
    expect(add.request.body).toEqual(request);
    add.flush(null);
    expect(added).toHaveBeenCalledTimes(1);
    lookup().flush(account);
    await settle();
  });
});
