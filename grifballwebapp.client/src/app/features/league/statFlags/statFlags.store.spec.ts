import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { StatFlagDto } from '@api/models';
import { StatFlagsStore } from './statFlags.store';

const flag: StatFlagDto = {
  statFlagID: 7, rule: 'NegativeStat', matchID: null, recordedGameID: 101, seasonMatchID: 12, gameNumber: 2,
  playerUserID: 5, playerName: 'Sarge', reason: '-1 kills', foundAt: '2026-11-20T12:00:00Z',
  gamePlayedAt: null, gameTeams: null,
};

describe('StatFlagsStore', () => {
  let store: StatFlagsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [StatFlagsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(StatFlagsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the open flags, and says when it fails, clearing that on a retry', () => {
    spyOn(console, 'error');
    expect(store.loading()).toBeTrue();
    store.load();
    http.expectOne('/api/stat-flags').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loading()).toBeFalse();
    expect(store.error()).toBe('Failed to load the open stat flags');

    store.load();
    expect(store.loading()).toBeTrue();
    expect(store.error()).toBeNull();
    http.expectOne('/api/stat-flags').flush([flag]);
    expect(store.loading()).toBeFalse();
    expect(store.flags()).toEqual([flag]);
  });

  it('reloading keeps the list shown while it loads again', () => {
    store.load();
    http.expectOne('/api/stat-flags').flush([flag]);
    expect(store.loading()).toBeFalse();

    store.load();

    expect(store.loading()).toBeFalse();
    expect(store.flags()).toEqual([flag]);
    http.expectOne('/api/stat-flags').flush([flag]);
  });

  it('dismisses a flag, dropping it from the list shown', () => {
    store.load();
    http.expectOne('/api/stat-flags').flush([flag]);

    store.dismiss(flag.statFlagID);
    http.expectOne({ method: 'POST', url: '/api/stat-flags/7/dismiss' }).flush(null);

    expect(store.flags()).toEqual([]);
  });

  it('dismissing before anything has loaded leaves the list null', () => {
    store.dismiss(flag.statFlagID);
    http.expectOne({ method: 'POST', url: '/api/stat-flags/7/dismiss' }).flush(null);

    expect(store.flags()).toBeNull();
  });

  it('logs, rather than throws, when dismissing fails', () => {
    spyOn(console, 'error');
    store.load();
    http.expectOne('/api/stat-flags').flush([flag]);

    store.dismiss(flag.statFlagID);
    http.expectOne('/api/stat-flags/7/dismiss').flush('x', { status: 500, statusText: 'Server Error' });

    expect(console.error).toHaveBeenCalled();
    expect(store.flags()).toEqual([flag]);
  });
});
