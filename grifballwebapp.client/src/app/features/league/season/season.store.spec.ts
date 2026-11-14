import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { SeasonSummaryDto } from '@api/models';
import { SeasonStore } from './season.store';

const summary = (seasonID: number) => ({ seasonID, seasonName: `Season ${seasonID}` }) as SeasonSummaryDto;

describe('SeasonStore', () => {
  let store: SeasonStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SeasonStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(SeasonStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.seasonName()).toBe('Season');
  });

  it("loads the route's season's summary, and its name with it", () => {
    store.seasonID.use(signal(4));
    TestBed.tick();
    expect(store.shownSeasonID()).toBe(4);
    expect(store.nameLoaded()).toBeFalse();
    http.expectOne('/api/seasons/4/summary').flush(summary(4));
    expect(store.summary()).toEqual(summary(4));
    expect(store.seasonName()).toBe('Season 4');
    expect(store.nameLoaded()).toBeTrue();
  });

  it('shows the current season when the route names none, and the name is in even when the summary fails', () => {
    store.seasonID.use(signal(0));
    TestBed.tick();
    http.expectOne('/api/seasons/current').flush(12);
    expect(store.shownSeasonID()).toBe(12);
    http.expectOne('/api/seasons/12/summary').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.summary()).toBeNull();
    expect(store.nameLoaded()).toBeTrue();
  });

  it("forgets the last season, and drops its late answers, for another", () => {
    const seasonID = signal(0);
    store.seasonID.use(seasonID);
    TestBed.tick();
    const current = http.expectOne('/api/seasons/current');
    seasonID.set(4);
    TestBed.tick();
    const late = http.expectOne('/api/seasons/4/summary');
    current.flush(12);
    expect(store.shownSeasonID()).toBe(4);

    seasonID.set(5);
    TestBed.tick();
    late.flush(summary(4));
    expect(store.summary()).toBeNull();
    expect(store.seasonName()).toBe('Season');
    http.expectOne('/api/seasons/5/summary').flush(summary(5));
    expect(store.seasonName()).toBe('Season 5');
  });

  it('loads the events, and is done with them even when they fail', () => {
    store.loadEvents();
    http.expectOne('/api/events').flush([{ seasonID: 4 }]);
    expect(store.events()).toHaveSize(1);
    expect(store.eventsLoaded()).toBeTrue();

    store.loadEvents();
    http.expectOne('/api/events').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.eventsLoaded()).toBeTrue();
  });
});
