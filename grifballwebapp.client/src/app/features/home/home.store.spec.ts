import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { seasonInfo } from '@shared/testing/seasonInfo.stub';
import { HomeStore, type PastSeasonsPage } from './home.store';
import type { SeasonsLiveDto } from '@api/models';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS } from '@core/live/liveReload';

/** The live service as the store sees it: who watches what, and a message to send them. */
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

describe('HomeStore', () => {
  let store: HomeStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [HomeStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }],
    });
    store = TestBed.inject(HomeStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const pastSeason = (id: number) => ({ ...seasonInfo(id, { seasonName: 'Past Event', start: '2023-01-01', end: '2023-01-31' }), teamCount: 0, playerCount: 0, champions: [], teamNames: [] });

  it('loads the current and coming events, a skeleton meanwhile', async () => {
    const events = [{ seasonID: 1, name: 'Test Event', start: '2024-01-01', end: '2024-01-31', eventType: 'Signup' as const }];
    TestBed.tick();
    expect(store.eventsFirstLoad()).toBeTrue();
    const req = http.expectOne('/api/events');
    expect(req.request.method).toBe('GET');
    req.flush(events);
    await settle();
    expect(store.events()).toEqual(events);
    expect(store.eventsFirstLoad()).toBeFalse();
  });

  it('is done with the events, showing none, when they fail', async () => {
    TestBed.tick();
    http.expectOne('/api/events').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.eventsFirstLoad()).toBeFalse();
    expect(store.events()).toEqual([]);
  });

  describe('past seasons', () => {
    beforeEach(async () => {
      TestBed.tick();
      http.expectOne('/api/events').flush([]);
      await settle();
    });

    function show(page: PastSeasonsPage): void {
      store.page.use(signal(page));
      TestBed.tick();
    }

    it('asks for the page, unsorted, as index.html fetches the first early, and shows it', async () => {
      show({ pageSize: 10, pageNumber: 1 });
      expect(store.pastSeasonsFirstLoad()).toBeTrue();
      const req = http.expectOne(r => r.url === '/api/seasons');
      expect(req.request.urlWithParams).toBe('/api/seasons?ended=true&pageNumber=1&pageSize=10');
      req.flush({ results: [pastSeason(3)], totalCount: 1 });
      await settle();
      expect(store.pastSeasons().map(s => s.seasonID)).toEqual([3]);
      expect(store.total()).toBe(1);
      expect(store.pastSeasonsFirstLoad()).toBeFalse();
    });

    it('asks for a page sorted descending', async () => {
      show({ pageSize: 25, pageNumber: 3, sortDirection: 'desc', sortColumn: 'seasonName' });
      const req = http.expectOne(r => r.url === '/api/seasons');
      expect(req.request.params.get('pageSize')).toBe('25');
      expect(req.request.params.get('pageNumber')).toBe('3');
      expect(req.request.params.get('sortDirection')).toBe('Desc');
      expect(req.request.params.get('sortColumn')).toBe('seasonName');
      req.flush({ results: [], totalCount: 0 });
      await settle();
    });

    it('keeps the page shown while the next loads, and when it fails', async () => {
      const page = signal<PastSeasonsPage>({ pageSize: 10, pageNumber: 1 });
      store.page.use(page);
      TestBed.tick();
      http.expectOne(r => r.url === '/api/seasons').flush({ results: [pastSeason(3)], totalCount: 11 });
      await settle();

      page.set({ pageSize: 10, pageNumber: 2 });
      TestBed.tick();
      expect(store.pastSeasons().map(s => s.seasonID)).toEqual([3]);
      expect(store.pastSeasonsFirstLoad()).toBeFalse();
      http.expectOne(r => r.url === '/api/seasons').flush(null, { status: 500, statusText: 'Server Error' });
      await settle();
      expect(store.pastSeasons().map(s => s.seasonID)).toEqual([3]);
      expect(store.total()).toBe(11);
      expect(store.pastSeasonsFailed()).toBeFalse();
    });

    it('fails, with no rows and no skeleton, when the first page cannot be read', async () => {
      show({ pageSize: 10, pageNumber: 1 });
      http.expectOne(r => r.url === '/api/seasons').flush(null, { status: 500, statusText: 'Server Error' });
      await settle();
      expect(store.pastSeasonsFirstLoad()).toBeFalse();
      expect(store.pastSeasonsFailed()).toBeTrue();
      expect(store.pastSeasons()).toEqual([]);
    });
  });

  describe('live', () => {
    const pushed: SeasonsLiveDto = { events: [{ seasonID: 7, name: 'Winter', eventType: 'Season', start: '2026-11-20T00:00:00Z', end: '2027-02-15T00:00:00Z' }] };

    beforeEach(() => {
      jasmine.clock().install();
      // No spread: a reload runs LIVE_RELOAD_MS after a change
      spyOn(Math, 'random').and.returnValue(0);
      TestBed.tick();
    });
    afterEach(() => jasmine.clock().uninstall());

    it('shows the events pushed with a change to the seasons, with no request of its own', () => {
      const first = http.expectOne('/api/events');
      expect(live.active).toEqual([['seasons']]);
      live.send({ topic: 'seasons', data: pushed });
      expect(store.events()).toEqual(pushed.events);
      expect(store.eventsFirstLoad()).toBeFalse();
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      TestBed.tick();
      http.expectNone('/api/events');
      if (!first.cancelled)
        first.flush(pushed.events);
    });

    it('loads the events again on a change without data, once it has them', async () => {
      const first = http.expectOne('/api/events');
      live.send({ topic: 'seasons' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      TestBed.tick();
      http.expectNone('/api/events');

      first.flush([]);
      // The value comes a microtask after the answer (the clock is mocked: no whenStable)
      await Promise.resolve();
      TestBed.tick();
      live.send({ topic: 'seasons' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      TestBed.tick();
      http.expectOne('/api/events').flush(pushed.events);
    });
  });
});
