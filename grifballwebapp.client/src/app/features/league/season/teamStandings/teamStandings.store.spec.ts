import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TeamStandingsStore } from './teamStandings.store';
import type { SeasonLiveDto } from '@api/models';
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

/** A season's page as the live hub pushes it: nothing but what a test gives. */
const seasonLive = (seasonID: number, overrides: Partial<SeasonLiveDto> = {}): SeasonLiveDto =>
  ({ seasonID, summary: null, standings: [], scheduled: [], unscheduled: [], playerStats: [], ...overrides });

describe('TeamStandingsStore', () => {
  let store: TeamStandingsStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [TeamStandingsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }],
    });
    store = TestBed.inject(TeamStandingsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing without a season, and is loaded', () => {
    store.seasonID.use(signal(0));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.loaded()).toBeTrue();
    expect(store.teamStandings()).toEqual([]);
  });

  it("loads the season's standings, cancelling the last season's", async () => {
    const seasonID = signal(2);
    store.seasonID.use(seasonID);
    TestBed.tick();
    const first = http.expectOne('/api/seasons/2/standings');
    expect(store.loaded()).toBeFalse();
    seasonID.set(3);
    TestBed.tick();
    expect(first.cancelled).toBeTrue();
    http.expectOne('/api/seasons/3/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    await settle();
    expect(store.teamStandings().map(t => t.teamName)).toEqual(['Alpha']);
    expect(store.loaded()).toBeTrue();
  });

  it('is loaded with none when they fail', async () => {
    spyOn(console, 'error');
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/seasons/2/standings').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.loaded()).toBeTrue();
    expect(store.teamStandings()).toEqual([]);
  });

  describe('live', () => {
    // The clock goes in once the first load is in (whenStable waits on the real one)
    beforeEach(async () => {
      store.seasonID.use(signal(4));
      TestBed.tick();
      http.expectOne('/api/seasons/4/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
      await TestBed.inject(ApplicationRef).whenStable();
      jasmine.clock().install();
      // No spread: a reload runs LIVE_RELOAD_MS after a change
      spyOn(Math, 'random').and.returnValue(0);
    });
    afterEach(() => jasmine.clock().uninstall());

    it('shows what is pushed with a change to the season, with no request', () => {
      expect(live.active).toEqual([['season:4']]);
      live.send({ topic: 'season:4', data: seasonLive(4, { standings: [{ teamID: 11, teamName: 'Bravo', wins: 5, losses: 0, finish: null, recordFromSource: false }] }) });
      expect(store.teamStandings().map(t => t.teamName)).toEqual(['Bravo']);
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      TestBed.tick();
      http.expectNone('/api/seasons/4/standings');
    });

    it("ignores another season's data", () => {
      live.send({ topic: 'season:4', data: seasonLive(5, { standings: [{ teamID: 11, teamName: 'Bravo', wins: 5, losses: 0, finish: null, recordFromSource: false }] }) });
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      TestBed.tick();
      expect(store.teamStandings().map(t => t.teamName)).toEqual(['Alpha']);
      http.expectNone(() => true);
    });

    it('reads it again on a change without data', () => {
      live.send({ topic: 'season:4' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      TestBed.tick();
      http.expectOne('/api/seasons/4/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    });
  });
});
