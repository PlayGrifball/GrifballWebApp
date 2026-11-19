import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, type TestRequest } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { QueueStatusDto } from '@api/models';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS } from '@core/live/liveReload';
import { LateLeagueQueueStore } from './lateLeagueQueue.store';

const player: QueueStatusDto = { count: 3, open: true, signedIn: true, hasGamertag: true, inQueue: false, inMatch: false };
const refused = [{ title: 'Refused', detail: 'The queue is closed.' }, { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } }] as const;

/** The live service as the page sees it: who watches what, and a message to send them. */
class FakeLive {
  readonly watching: { topics: readonly string[], listener: (message: LiveMessage) => void, stopped: boolean }[] = [];

  watch(topics: readonly string[], listener: (message: LiveMessage) => void): () => void {
    const watch = { topics, listener, stopped: false };
    this.watching.push(watch);
    return () => watch.stopped = true;
  }

  /** Sends `message` to those watching its topic; a null topic (the connection came back) to everyone. */
  send(message: LiveMessage): void {
    for (const watch of this.watching.filter(w => !w.stopped && (message.topic === null || w.topics.includes(message.topic))))
      watch.listener(message);
  }

  get active(): (readonly string[])[] {
    return this.watching.filter(w => !w.stopped).map(w => w.topics);
  }
}

describe('LateLeagueQueueStore', () => {
  let store: LateLeagueQueueStore;
  let http: HttpTestingController;

  let live: FakeLive;

  // On the server: nothing watched and no first load, so each test drives the store itself (the browser's tests below cover live changes)
  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [
        LateLeagueQueueStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: PLATFORM_ID, useValue: 'server' },
        { provide: LiveService, useValue: live },
      ],
    });
    store = TestBed.inject(LateLeagueQueueStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const status = () => http.expectOne({ method: 'GET', url: '/api/queue' });

  it('loads nothing on the server, and watches nothing', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.status()).toBeNull();
    expect(live.watching).toEqual([]);
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

describe('LateLeagueQueueStore in the browser', () => {
  let store: LateLeagueQueueStore;
  let http: HttpTestingController;
  let live: FakeLive;
  /** The store's first status request, made as it starts; each test answers it. */
  let firstLoad: TestRequest;

  beforeEach(() => {
    jasmine.clock().install();
    // No spread: a reload runs LIVE_RELOAD_MS after a change
    spyOn(Math, 'random').and.returnValue(0);
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [
        LateLeagueQueueStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: LiveService, useValue: live },
      ],
    });
    store = TestBed.inject(LateLeagueQueueStore);
    http = TestBed.inject(HttpTestingController);
    TestBed.tick();
    firstLoad = status();
  });

  afterEach(() => {
    http.verify();
    jasmine.clock().uninstall();
  });

  const status = () => http.expectOne({ method: 'GET', url: '/api/queue' });
  /** Long enough for any load a change could start. */
  const waitOut = () => jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);

  it('watches the queue\'s size, and the viewer\'s own place in it', () => {
    firstLoad.flush(player);
    expect(live.active).toEqual([['queue'], ['queue:me']]);
  });

  it('shows a pushed count at once, with no request', () => {
    firstLoad.flush(player);
    live.send({ topic: 'queue', data: { count: 7 } });
    expect(store.status()).toEqual({ ...player, count: 7 });
    waitOut();
    http.expectNone('/api/queue');
  });

  it('loads the status when a count is pushed before any status is loaded', () => {
    firstLoad.flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.status()).toBeNull();

    live.send({ topic: 'queue', data: { count: 7 } });
    jasmine.clock().tick(LIVE_RELOAD_MS);
    status().flush({ ...player, count: 7 });
    expect(store.status()!.count).toBe(7);
  });

  it('loads the status once a burst of changes to the viewer\'s own place settles', () => {
    firstLoad.flush(player);
    live.send({ topic: 'queue:me' });
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    live.send({ topic: 'queue:me' });
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    http.expectNone('/api/queue');

    jasmine.clock().tick(1);
    status().flush({ ...player, inMatch: true });
    expect(store.status()!.inMatch).toBeTrue();
  });

  it('loads the status when the live connection comes back', () => {
    firstLoad.flush(player);
    live.send({ topic: null });
    jasmine.clock().tick(LIVE_RELOAD_MS);
    // Each watch loads; only the latest answer is taken
    for (const request of http.match({ method: 'GET', url: '/api/queue' }))
      request.flush({ ...player, count: 4 });
    expect(store.status()!.count).toBe(4);
  });

  it('loads after a change to the viewer\'s place only once a join under way is done', () => {
    firstLoad.flush(player);
    store.join();
    live.send({ topic: 'queue:me' });
    waitOut();
    http.expectNone('/api/queue');

    http.expectOne({ method: 'POST', url: '/api/queue/join' }).flush({ ...player, inQueue: true, count: 4 });
    status().flush({ ...player, inQueue: true, count: 5 });
    expect(store.status()!.count).toBe(5);
  });

  it('loads after a change to the viewer\'s place only once a leave under way is done', () => {
    firstLoad.flush({ ...player, inQueue: true });
    store.leave();
    live.send({ topic: 'queue:me' });
    waitOut();
    http.expectNone('/api/queue');

    http.expectOne({ method: 'POST', url: '/api/queue/leave' }).flush(player);
    status().flush({ ...player, count: 2 });
    expect(store.status()!.count).toBe(2);
  });

  it('stops watching when it goes', () => {
    firstLoad.flush(player);
    TestBed.resetTestingModule();
    expect(live.active).toEqual([]);
  });
});
