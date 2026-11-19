import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { seasonInfo } from '@shared/testing/seasonInfo.stub';
import { HomeStore } from './home.store';
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

  it('is done with the events even when they fail', () => {
    store.loadEvents();
    http.expectOne('/api/events').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.eventsLoaded()).toBeTrue();
    expect(store.events()).toEqual([]);
  });

  it('loads the current and coming events', () => {
    const events = [{ seasonID: 1, name: 'Test Event', start: '2024-01-01', end: '2024-01-31', eventType: 'Signup' as const }];
    store.loadEvents();
    const req = http.expectOne('/api/events');
    expect(req.request.method).toBe('GET');
    req.flush(events);
    expect(store.events()).toEqual(events);
    expect(store.eventsLoaded()).toBeTrue();
  });

  describe('loadPastSeasons', () => {
    it('should make GET request with pagination parameters', (done) => {
      const mockResult = {
        results: [
          { ...seasonInfo(3, { seasonName: 'Past Event', start: '2023-01-01', end: '2023-01-31' }), teamCount: 0, playerCount: 0, champions: [], teamNames: [] }
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10
      };

      store.loadPastSeasons(10, 1, undefined, undefined).subscribe({
        next: (result) => {
          expect(result).toEqual(mockResult);
          expect(store.total()).toBe(1);
          expect(store.pastSeasons()).toEqual(mockResult.results);
          done();
        }
      });

      const req = http.expectOne((request) => 
        request.url === '/api/seasons' &&
        request.params.get('pageSize') === '10' &&
        request.params.get('pageNumber') === '1'
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockResult);
    });

    it('should include sort parameters when provided', (done) => {
      const mockResult = {
        results: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 10
      };

      store.loadPastSeasons(10, 1, 'asc', 'seasonName').subscribe({
        next: () => done()
      });

      const req = http.expectOne((request) => 
        request.url === '/api/seasons' &&
        request.params.get('sortDirection') === 'Asc' &&
        request.params.get('sortColumn') === 'seasonName'
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockResult);
    });

    it('should not include sort parameters when not provided', (done) => {
      const mockResult = {
        results: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 10
      };

      store.loadPastSeasons(10, 1, undefined, undefined).subscribe({
        next: result => {
          expect(result).toEqual(mockResult);
          done();
        }
      });

      const req = http.expectOne((request) => {
        return request.url === '/api/seasons' &&
               !request.params.has('sortDirection') &&
               !request.params.has('sortColumn');
      });
      req.flush(mockResult);
    });

    it('asks for a page sorted descending', () => {
      store.loadPastSeasons(25, 3, 'desc', 'seasonName').subscribe();
      const req = http.expectOne(r => r.url === '/api/seasons');
      expect(req.request.params.get('pageSize')).toBe('25');
      expect(req.request.params.get('pageNumber')).toBe('3');
      expect(req.request.params.get('sortDirection')).toBe('Desc');
      req.flush({ results: [], totalCount: 0 });
    });

    it('keeps the page shown, and stays usable, when a page fails', () => {
      const shown = { ...seasonInfo(3, { seasonName: 'Past Event', start: '2023-01-01', end: '2023-01-31' }), teamCount: 0, playerCount: 0, champions: [], teamNames: [] };
      store.loadPastSeasons(10, 1, undefined, undefined).subscribe();
      http.expectOne(r => r.url === '/api/seasons').flush({ results: [shown], totalCount: 11 });

      const failed = jasmine.createSpy('failed');
      // A failure must not reach the paging stream: it would end it, and the table would page no more
      store.loadPastSeasons(10, 2, undefined, undefined).subscribe({ error: failed });
      http.expectOne(r => r.url === '/api/seasons').flush(null, { status: 500, statusText: 'Server Error' });
      expect(failed).not.toHaveBeenCalled();
      expect(store.pastSeasons()).toEqual([shown]);
      expect(store.total()).toBe(11);
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

    it('shows the events pushed with a change to the seasons, with no request', () => {
      expect(live.active).toEqual([['seasons']]);
      live.send({ topic: 'seasons', data: pushed });
      expect(store.events()).toEqual(pushed.events);
      expect(store.eventsLoaded()).toBeTrue();
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      http.expectNone('/api/events');
    });

    it('loads the events again on a change without data, once it has them', () => {
      live.send({ topic: 'seasons' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectNone('/api/events');

      store.loadEvents();
      http.expectOne('/api/events').flush([]);
      live.send({ topic: 'seasons' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectOne('/api/events').flush(pushed.events);
      expect(store.events()).toEqual(pushed.events);
    });
  });
});
