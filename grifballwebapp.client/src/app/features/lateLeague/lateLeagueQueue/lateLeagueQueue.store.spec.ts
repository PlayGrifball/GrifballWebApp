import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { QueueStatusDto } from '@api/models';
import { LateLeagueQueueStore } from './lateLeagueQueue.store';

const player: QueueStatusDto = { count: 3, open: true, signedIn: true, hasGamertag: true, inQueue: false, inMatch: false };
const refused = [{ title: 'Refused', detail: 'The queue is closed.' }, { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('LateLeagueQueueStore', () => {
  let store: LateLeagueQueueStore;
  let http: HttpTestingController;

  // On the server: no hub and no first load, so each test drives the store itself (the page's spec covers the hub)
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [LateLeagueQueueStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: PLATFORM_ID, useValue: 'server' }],
    });
    store = TestBed.inject(LateLeagueQueueStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const status = () => http.expectOne({ method: 'GET', url: '/api/queue' });

  it('loads nothing on the server', () => {
    http.expectNone(() => true);
    expect(store.status()).toBeNull();
  });

  it('shows only the latest status, and says it failed only when it has none', () => {
    store.load();
    const older = status();
    store.load();
    status().flush({ ...player, count: 5 });
    older.flush(player);
    expect(store.status()!.count).toBe(5);

    store.load();
    status().flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.failed()).toBeFalse();
  });

  it('says the status failed when it has none', () => {
    store.load();
    status().flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.failed()).toBeTrue();
  });

  it("takes the hub's size, loading the status again for a player in the queue or a match", () => {
    store.counted(4);
    status().flush(player);

    store.counted(3);
    store.counted(6);
    http.expectNone('/api/queue');
    expect(store.status()!.count).toBe(6);

    store.load();
    status().flush({ ...player, inQueue: true, count: 6 });
    store.counted(7);
    status().flush({ ...player, inQueue: true, count: 7 });
  });

  it('joins and leaves, saying so, and loads a status asked for meanwhile once done', () => {
    store.join();
    expect(store.busy()).toBeTrue();
    store.load();
    http.expectOne({ method: 'POST', url: '/api/queue/join' }).flush({ ...player, inQueue: true });
    expect(store.busy()).toBeFalse();
    expect(store.message()).toBe('You have joined the Late League queue');
    status().flush({ ...player, inQueue: true });

    store.leave();
    http.expectOne({ method: 'POST', url: '/api/queue/leave' }).flush(player);
    expect(store.message()).toBe('You have left the Late League queue');
    expect(store.status()!.inQueue).toBeFalse();
  });

  it('says why a join was refused, and loads the status again', () => {
    store.join();
    http.expectOne('/api/queue/join').flush(...refused);
    expect(store.message()).toBe('The queue is closed.');
    expect(store.busy()).toBeFalse();
    status().flush(player);
  });
});
