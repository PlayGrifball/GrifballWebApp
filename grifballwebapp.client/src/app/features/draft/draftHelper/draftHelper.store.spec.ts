import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { DraftPoolPlayer, DraftSuggestions } from '@api/models';
import { DraftHelperStore } from './draftHelper.store';

describe('DraftHelperStore', () => {
  let store: DraftHelperStore;
  let http: HttpTestingController;
  const pool: DraftPoolPlayer[] = [{ userID: 7, name: 'Ace', region: 'West', willCaptain: false, timeslots: [], career: null }];
  const suggestions: DraftSuggestions = { teamID: 1, teamName: 'Reds', captainName: 'Cap', picks: [{ userID: 7, name: 'Ace', score: 0.68, reasons: ['Won 80% of 20 games'] }] };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [DraftHelperStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(DraftHelperStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads the season's pool in detail and the suggested picks", () => {
    store.load(5);
    http.expectOne('/api/seasons/5/draft/pool-details').flush(pool);
    http.expectOne('/api/seasons/5/draft/suggestions').flush(suggestions);

    expect(store.pool()).toEqual(pool);
    expect(store.suggestions()).toEqual(suggestions);
  });

  it('has no suggestions without a team to make them for, the pool all the same; a later season replaces an earlier', () => {
    store.load(5);
    store.load(6);
    http.expectOne('/api/seasons/6/draft/pool-details').flush(pool);
    http.expectOne('/api/seasons/6/draft/suggestions').flush({ title: 'Not Found', status: 404 }, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/seasons/5/draft/pool-details').flush([]);
    http.expectOne('/api/seasons/5/draft/suggestions').flush(suggestions);

    expect(store.pool()).toEqual(pool);
    expect(store.suggestions()).toBeNull();
  });

  it('shows an empty pool when the pool fails to load, the suggestions all the same', () => {
    store.load(5);
    http.expectOne('/api/seasons/5/draft/pool-details').flush('boom', { status: 500, statusText: 'Server Error' });
    http.expectOne('/api/seasons/5/draft/suggestions').flush(suggestions);

    expect(store.pool()).toEqual([]);
    expect(store.suggestions()).toEqual(suggestions);
  });
});
