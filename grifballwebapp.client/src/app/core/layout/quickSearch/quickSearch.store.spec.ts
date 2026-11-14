import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { QuickSearchStore } from './quickSearch.store';

const hits = { players: [], teams: [], seasons: [] };

describe('QuickSearchStore', () => {
  let store: QuickSearchStore;
  let http: HttpTestingController;
  const term = signal('');

  beforeEach(() => {
    term.set('');
    TestBed.configureTestingModule({ providers: [QuickSearchStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(QuickSearchStore);
    http = TestBed.inject(HttpTestingController);
    store.term.use(term);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const search = (q: string) => {
    TestBed.tick();
    return http.expectOne(r => r.url === '/api/search' && r.params.get('q') === q);
  };

  it('searches nothing shorter than the server searches', () => {
    term.set('G');
    TestBed.tick();
    http.expectNone('/api/search');
    expect(store.hits()).toBeNull();
    expect(store.loading()).toBeFalse();
  });

  it('searches the term, and again on a retry after it failed', async () => {
    term.set('Gr');
    search('Gr').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
    expect(store.hits()).toBeNull();

    store.retry();
    search('Gr').flush(hits);
    await settle();
    expect(store.failed()).toBeFalse();
    expect(store.hits()).toEqual(hits as never);
  });

  it('drops the search for a term typed past', async () => {
    term.set('Gr');
    const first = search('Gr');
    term.set('Grif');
    const second = search('Grif');
    expect(first.cancelled).toBeTrue();
    second.flush(hits);
    await settle();
    expect(store.loading()).toBeFalse();
  });
});
