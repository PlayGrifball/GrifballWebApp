import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LateLeagueStore } from './lateLeague.store';

describe('LateLeagueStore', () => {
  let store: LateLeagueStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [LateLeagueStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(LateLeagueStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.detail()).toBeNull();
  });

  it("loads the queue's players and the seasons without a season, one failing without the other", async () => {
    store.seasonID.use(signal(null));
    TestBed.tick();
    http.expectOne('/api/late-league/queue').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeFalse();
    http.expectOne('/api/late-league/seasons').flush([{ matchmakingSeasonID: 2 }]);
    await settle();
    expect(store.loaded()).toBeTrue();
    expect(store.queue()).toBeNull();
    expect(store.seasons()).toHaveSize(1);
    expect(store.detail()).toBeNull();
  });

  it("loads a season's players with a season, and the overview again without one", async () => {
    const seasonID = signal<number | null>(2);
    store.seasonID.use(seasonID);
    TestBed.tick();
    http.expectOne('/api/late-league/seasons/2').flush({ matchmakingSeasonID: 2, players: [] });
    await settle();
    expect(store.detail()).toEqual({ matchmakingSeasonID: 2, players: [] } as never);
    expect(store.seasons()).toBeNull();

    seasonID.set(null);
    TestBed.tick();
    http.expectOne('/api/late-league/queue').flush({ players: [] });
    http.expectOne('/api/late-league/seasons').flush([]);
    await settle();
    expect(store.detail()).toBeNull();
    expect(store.seasons()).toEqual([]);
  });
});
