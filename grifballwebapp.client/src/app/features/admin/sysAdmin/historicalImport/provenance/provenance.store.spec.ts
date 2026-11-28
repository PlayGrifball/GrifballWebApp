import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ProvenanceQuery, ProvenanceStore } from './provenance.store';

describe('ProvenanceStore', () => {
  let store: ProvenanceStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ProvenanceStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(ProvenanceStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks nothing until a lookup is asked, then the entity\'s rows', async () => {
    const query = signal<ProvenanceQuery | null>(null);
    store.query.use(query);
    TestBed.tick();
    http.expectNone(r => r.url.includes('historical-provenance'));

    query.set({ entity: 'User', key: '4182' });
    TestBed.tick();
    const request = http.expectOne(r => r.url === '/api/historical-provenance');
    expect(request.request.params.get('entity')).toBe('User');
    expect(request.request.params.get('key')).toBe('4182');
    request.flush([{ historicalProvenanceID: 1 }]);
    await settle();

    expect(store.loaded()).toBeTrue();
    expect(store.rows()).toHaveSize(1);
  });

  it('says when the lookup fails', async () => {
    store.query.use(signal<ProvenanceQuery | null>({ entity: 'Team', key: '7' }));
    TestBed.tick();
    http.expectOne(r => r.url === '/api/historical-provenance').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();

    expect(store.failed()).toBeTrue();
    expect(store.rows()).toEqual([]);
  });
});
