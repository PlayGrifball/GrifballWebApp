import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatchDetailsStore } from './matchDetails.store';

const raw = { matchID: 'm-1', startTime: '2026-10-30T19:00:00Z', endTime: '2026-10-30T19:10:00Z', duration: '00:10:00', teams: [], medals: [], seasonMatch: null };

describe('MatchDetailsStore', () => {
  let store: MatchDetailsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [MatchDetailsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(MatchDetailsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the match ID, and says it is loading meanwhile', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.details()).toBeNull();
    // Not "no such match": nothing has been asked for yet
    expect(store.state()).toBe('loading');
  });

  it('loads the match, and another in its place, cancelling the first', async () => {
    const matchID = signal('m-1');
    store.matchID.use(matchID);
    TestBed.tick();
    const first = http.expectOne('/api/matches/m-1');
    expect(store.state()).toBe('loading');
    matchID.set('m-2');
    TestBed.tick();
    expect(first.cancelled).toBeTrue();
    http.expectOne('/api/matches/m-2').flush({ ...raw, matchID: 'm-2' });
    await settle();
    expect(store.state()).toBe('loaded');
    expect(store.details()!.matchID).toBe('m-2');
  });

  it('tells no such match (none, 404 or 400) from a failed request', async () => {
    const matchID = signal('m-1');
    store.matchID.use(matchID);
    TestBed.tick();
    http.expectOne('/api/matches/m-1').flush(null);
    await settle();
    expect(store.state()).toBe('notFound');

    for (const [id, status, state] of [['m-2', 404, 'notFound'], ['m-3', 400, 'notFound'], ['m-4', 500, 'error']] as const) {
      matchID.set(id);
      TestBed.tick();
      http.expectOne(`/api/matches/${id}`).flush('x', { status, statusText: 'Refused' });
      await settle();
      expect(store.state()).toBe(state);
      expect(store.details()).toBeNull();
    }
  });
});
