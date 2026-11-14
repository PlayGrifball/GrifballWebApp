import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SeasonThisWeekStore } from './seasonThisWeek.store';

describe('SeasonThisWeekStore', () => {
  let store: SeasonThisWeekStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SeasonThisWeekStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(SeasonThisWeekStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for nothing without a season', () => {
    store.seasonID.use(signal(0));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.matches()).toEqual([]);
  });

  it("loads the season's scheduled matches, their times read, and has none when they fail", async () => {
    const seasonID = signal(4);
    store.seasonID.use(seasonID);
    TestBed.tick();
    http.expectOne('/api/seasons/4/matches/scheduled').flush([
      { seasonMatchID: 1, homeCaptain: 'H', awayCaptain: 'A', homeTeam: 'Home', awayTeam: 'Away', complete: false, time: '2026-01-07T19:00:00Z' },
    ]);
    await TestBed.inject(ApplicationRef).whenStable();
    expect(store.matches()[0].time.toUTC().toISO()).toBe('2026-01-07T19:00:00.000Z');

    seasonID.set(5);
    TestBed.tick();
    http.expectOne('/api/seasons/5/matches/scheduled').flush('x', { status: 500, statusText: 'Server Error' });
    await TestBed.inject(ApplicationRef).whenStable();
    expect(store.matches()).toEqual([]);
  });
});
