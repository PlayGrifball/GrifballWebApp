import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
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

  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const answer = (seasonID: number, p: DraftPoolPlayer[] | null, s: DraftSuggestions | null) => {
    const poolReq = http.expectOne(`/api/seasons/${seasonID}/draft/pool-details`);
    if (p) poolReq.flush(p); else poolReq.flush('boom', { status: 500, statusText: 'Server Error' });
    const suggestionsReq = http.expectOne(`/api/seasons/${seasonID}/draft/suggestions`);
    if (s) suggestionsReq.flush(s); else suggestionsReq.flush({ title: 'Not Found', status: 404 }, { status: 404, statusText: 'Not Found' });
  };

  it("loads the season's pool in detail and the suggested picks, a skeleton meanwhile", async () => {
    store.seasonID.use(signal(5));
    store.draftKey.use(signal('7'));
    TestBed.tick();
    expect(store.firstLoad()).toBeTrue();
    answer(5, pool, suggestions);
    await settle();
    expect(store.firstLoad()).toBeFalse();
    expect(store.pool()).toEqual(pool);
    expect(store.suggestions()).toEqual(suggestions);
  });

  it('reads it again when the draft changes, keeping the last answer shown meanwhile', async () => {
    const draftKey = signal('7');
    store.seasonID.use(signal(5));
    store.draftKey.use(draftKey);
    TestBed.tick();
    answer(5, pool, suggestions);
    await settle();

    draftKey.set('');
    TestBed.tick();
    expect(store.firstLoad()).toBeFalse();
    expect(store.pool()).toEqual(pool);
    answer(5, [], null);
    await settle();
    expect(store.pool()).toEqual([]);
    expect(store.suggestions()).toBeNull();
  });

  it('starts over for another season', async () => {
    const seasonID = signal(5);
    store.seasonID.use(seasonID);
    store.draftKey.use(signal(''));
    TestBed.tick();
    answer(5, pool, suggestions);
    await settle();

    seasonID.set(6);
    TestBed.tick();
    expect(store.pool()).toEqual([]);
    expect(store.firstLoad()).toBeTrue();
    answer(6, pool, null);
    await settle();
    expect(store.pool()).toEqual(pool);
    expect(store.suggestions()).toBeNull();
  });

  it('shows an empty pool when the pool fails to load, the suggestions all the same', async () => {
    store.seasonID.use(signal(5));
    store.draftKey.use(signal(''));
    TestBed.tick();
    answer(5, null, suggestions);
    await settle();
    expect(store.pool()).toEqual([]);
    expect(store.suggestions()).toEqual(suggestions);
    expect(store.firstLoad()).toBeFalse();
  });
});
