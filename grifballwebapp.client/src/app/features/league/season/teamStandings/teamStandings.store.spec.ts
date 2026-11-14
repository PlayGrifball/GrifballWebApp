import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TeamStandingsStore } from './teamStandings.store';

describe('TeamStandingsStore', () => {
  let store: TeamStandingsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [TeamStandingsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(TeamStandingsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing without a season, and is loaded', () => {
    store.seasonID.use(signal(0));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.loaded()).toBeTrue();
    expect(store.teamStandings()).toEqual([]);
  });

  it("loads the season's standings, cancelling the last season's", async () => {
    const seasonID = signal(2);
    store.seasonID.use(seasonID);
    TestBed.tick();
    const first = http.expectOne('/api/seasons/2/standings');
    expect(store.loaded()).toBeFalse();
    seasonID.set(3);
    TestBed.tick();
    expect(first.cancelled).toBeTrue();
    http.expectOne('/api/seasons/3/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    await settle();
    expect(store.teamStandings().map(t => t.teamName)).toEqual(['Alpha']);
    expect(store.loaded()).toBeTrue();
  });

  it('is loaded with none when they fail', async () => {
    spyOn(console, 'log');
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/seasons/2/standings').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.loaded()).toBeTrue();
    expect(store.teamStandings()).toEqual([]);
  });
});
