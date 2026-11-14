import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { ProfileStatsDto } from '@api/models';
import { AccountService } from '@core/auth/account.service';
import { ProfileStore } from './profile.store';

describe('ProfileStore', () => {
  let store: ProfileStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  /** The signed-in user. */
  const me = signal(0);

  const stats = (userID: number, gamertag: string | null) => ({ userID, gamertag, displayName: 'Pete', seasons: [], championships: [] }) as unknown as ProfileStatsDto;

  beforeEach(() => {
    me.set(0);
    snackBar = jasmine.createSpyObj<MatSnackBar>('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [
        ProfileStore,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AccountService, useValue: { personID: me } },
      ],
    });
    store = TestBed.inject(ProfileStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows a player\'s gamertag and stats, loading until the stats are in', () => {
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'Grif' });
    expect(store.statsLoading()).toBeTrue();
    http.expectOne('/api/users/21/stats').flush(stats(21, 'Grif'));
    expect(store.gamertag()).toBe('Grif');
    expect(store.profileStats()!.displayName).toBe('Pete');
    expect(store.statsLoading()).toBeFalse();
    expect(store.settingGamertag()).toBeFalse();
  });

  it('asks you for your gamertag on your own profile without one, never on someone else\'s', () => {
    me.set(21);
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/21/stats').flush(stats(21, null));
    expect(store.settingGamertag()).toBeTrue();

    store.show(22);
    http.expectOne('/api/users/22/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/22/stats').flush(stats(22, null));
    expect(store.settingGamertag()).toBeFalse();
  });

  it('takes the gamertag from the stats when it has none of its own', () => {
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/21/stats').flush(stats(21, 'Simmons'));
    expect(store.gamertag()).toBe('Simmons');
  });

  it('sets your gamertag, then loads the stats again (it can merge accounts); says why when refused', () => {
    me.set(21);
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/21/stats').flush(stats(21, null));

    store.gamertag.set('Grif');
    store.setGamertag();
    const refused = http.expectOne({ method: 'POST', url: '/api/users/21/gamertag' });
    expect(refused.request.body).toEqual({ gamertag: 'Grif' });
    // A ProblemDetails, as text (the call reads its answer as text)
    refused.flush('{"title":"Request refused","detail":"Taken"}', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Taken', 'Close');
    expect(store.settingGamertag()).toBeTrue();

    store.setGamertag();
    http.expectOne({ method: 'POST', url: '/api/users/21/gamertag' }).flush(null);
    expect(store.settingGamertag()).toBeFalse();
    http.expectOne('/api/users/21/stats').flush(stats(21, 'Grif'));
  });

  it('drops the previous player and a late answer for them', () => {
    store.show(21);
    const lateTag = http.expectOne('/api/users/21/gamertag');
    const lateStats = http.expectOne('/api/users/21/stats');
    store.show(22);
    lateTag.flush({ gamertag: 'Old' });
    lateStats.flush(stats(21, 'Old'));
    expect(store.gamertag()).toBeNull();
    expect(store.profileStats()).toBeNull();
    http.expectOne('/api/users/22/gamertag').flush({ gamertag: 'New' });
    http.expectOne('/api/users/22/stats').flush(stats(22, 'New'));
    expect(store.gamertag()).toBe('New');
  });
});
