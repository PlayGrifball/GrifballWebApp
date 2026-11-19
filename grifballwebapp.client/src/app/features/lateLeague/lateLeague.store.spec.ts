import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LateLeagueStore } from './lateLeague.store';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS } from '@core/live/liveReload';

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

describe('LateLeagueStore', () => {
  let store: LateLeagueStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [LateLeagueStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }],
    });
    store = TestBed.inject(LateLeagueStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.detail()).toBeNull();
    // Nothing has been asked for yet: not done loading
    expect(store.loaded()).toBeFalse();
  });

  it("loads the queue's players and the seasons without a season, one failing without the other", async () => {
    store.seasonID.use(signal(null));
    TestBed.tick();
    http.expectOne('/api/late-league/queue').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeFalse();
    http.expectOne('/api/late-league/seasons').flush([{ matchmakingSeasonID: 2 }]);
    await settle();
    expect(store.loaded()).toBeTrue();
    expect(store.queue()).toBeNull();
    expect(store.seasons()).toHaveSize(1);
    expect(store.detail()).toBeNull();
  });

  it("loads a season's players with a season, and the overview again without one", async () => {
    const seasonID = signal<number | null>(2);
    store.seasonID.use(seasonID);
    TestBed.tick();
    http.expectOne('/api/late-league/seasons/2').flush({ matchmakingSeasonID: 2, players: [] });
    await settle();
    expect(store.detail()).toEqual({ matchmakingSeasonID: 2, players: [] } as never);
    expect(store.seasons()).toBeNull();

    seasonID.set(null);
    TestBed.tick();
    http.expectOne('/api/late-league/queue').flush({ players: [] });
    http.expectOne('/api/late-league/seasons').flush([]);
    await settle();
    expect(store.detail()).toBeNull();
    expect(store.seasons()).toEqual([]);
  });

  describe('live', () => {
    // The clock goes in only around the live change: whenStable waits on the real one
    afterEach(() => jasmine.clock().uninstall());
    /** A change to `topic`, and the store's wait for more changes run out (no spread). */
    const change = (topic: string) => {
      jasmine.clock().install();
      spyOn(Math, 'random').and.returnValue(0);
      live.send({ topic });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      jasmine.clock().uninstall();
      TestBed.tick();
    };

    it("loads the queue's players and the seasons again, in place, when Late League or its queue changes", async () => {
      store.seasonID.use(signal(null));
      TestBed.tick();
      http.expectOne('/api/late-league/queue').flush({ players: [] });
      http.expectOne('/api/late-league/seasons').flush([]);
      await settle();
      expect(live.active).toEqual([['lateleague', 'queue']]);

      change('queue');
      expect(store.seasons()).toEqual([]);
      http.expectOne('/api/late-league/queue').flush({ players: [] });
      http.expectOne('/api/late-league/seasons').flush([{ matchmakingSeasonID: 2 }]);
      await settle();
      expect(store.seasons()).toHaveSize(1);
    });

    it("loads a season's players again when Late League changes", async () => {
      store.seasonID.use(signal(2));
      TestBed.tick();
      http.expectOne('/api/late-league/seasons/2').flush({ matchmakingSeasonID: 2, players: [] });
      await settle();

      change('lateleague');
      http.expectOne('/api/late-league/seasons/2').flush({ matchmakingSeasonID: 2, players: [] });
      await settle();
      expect(store.detail()).toEqual({ matchmakingSeasonID: 2, players: [] } as never);
    });
  });
});
