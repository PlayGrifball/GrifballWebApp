import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { QuickSearchPlayerDto } from '@api/models';
import { PlayerSearchStore, SEARCH_DEBOUNCE_MS } from './playerSearch.store';

const player = (userID: number, name: string) => ({ userID, name }) as QuickSearchPlayerDto;

describe('PlayerSearchStore', () => {
  let store: PlayerSearchStore;
  let http: HttpTestingController;

  beforeEach(() => {
    jasmine.clock().install();
    TestBed.configureTestingModule({ providers: [PlayerSearchStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(PlayerSearchStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    jasmine.clock().uninstall();
  });

  const searched = (text: string) => http.expectOne(r => r.url === '/api/leaderboards/players' && r.params.get('query') === text);

  it('searches once typing pauses, only for the last text, and keeps what the results are for', () => {
    store.search('Le');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS - 1);
    store.search('Leg');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS - 1);
    http.expectNone('/api/leaderboards/players');
    jasmine.clock().tick(1);
    searched('Leg').flush([player(1, 'Legendary')]);
    expect(store.results().map(p => p.name)).toEqual(['Legendary']);
    expect(store.resultsFor()).toBe('Leg');
  });

  it('empties the results at once for short text, and for a cancel, dropping the request in flight', () => {
    store.search('Leg');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched('Leg').flush([player(1, 'Legendary')]);

    store.search('L');
    expect(store.results()).toEqual([]);
    expect(store.resultsFor()).toBe('L');

    store.search('Leg');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    const pending = searched('Leg');
    store.cancel();
    expect(pending.cancelled).toBeTrue();
    expect(store.results()).toEqual([]);
  });

  it('finds no one when the search fails', () => {
    store.search('Leg');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched('Leg').flush('down', { status: 500, statusText: 'Server Error' });
    expect(store.results()).toEqual([]);
    expect(store.resultsFor()).toBe('Leg');
  });

  it('gives a new list with each answer, empty too, so the list shown resets', () => {
    store.search('L');
    const first = store.results();
    store.search('L');
    expect(store.results()).not.toBe(first);
  });
});
