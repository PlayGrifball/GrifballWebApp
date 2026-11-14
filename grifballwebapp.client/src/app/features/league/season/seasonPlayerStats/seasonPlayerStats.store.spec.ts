import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { SeasonPlayerStatDto } from '@api/models';
import { SeasonPlayerStatsStore } from './seasonPlayerStats.store';

const row = (gamertag: string) => ({ gamertag }) as SeasonPlayerStatDto;

describe('SeasonPlayerStatsStore', () => {
  let store: SeasonPlayerStatsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SeasonPlayerStatsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(SeasonPlayerStatsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const request = (seasonID: number) => http.expectOne(r => r.url === `/api/seasons/${seasonID}/player-stats`);

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.stats()).toEqual([]);
  });

  it("loads the season's stats, then one team's, starting over for each", async () => {
    const teamID = signal<number | null>(null);
    store.seasonID.use(signal(4));
    store.teamID.use(teamID);
    TestBed.tick();
    const season = request(4);
    expect(season.request.params.has('teamID')).toBeFalse();
    expect(store.loaded()).toBeFalse();
    season.flush([row('Grif')]);
    await settle();
    expect(store.stats().map(r => r.gamertag)).toEqual(['Grif']);
    expect(store.loaded()).toBeTrue();

    teamID.set(10);
    TestBed.tick();
    expect(store.stats()).toEqual([]);
    const team = request(4);
    expect(team.request.params.get('teamID')).toBe('10');
    team.flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.stats()).toEqual([]);
    expect(store.loaded()).toBeTrue();
  });
});
