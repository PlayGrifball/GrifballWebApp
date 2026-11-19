import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SeasonThisWeekStore } from './seasonThisWeek.store';
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

describe('SeasonThisWeekStore', () => {
  let store: SeasonThisWeekStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [SeasonThisWeekStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }],
    });
    store = TestBed.inject(SeasonThisWeekStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for nothing without a season', () => {
    store.seasonID.use(signal(0));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.matches()).toEqual([]);
  });

  it("loads the season's scheduled matches, their times read, and has none when they fail", async () => {
    const seasonID = signal(4);
    store.seasonID.use(seasonID);
    TestBed.tick();
    http.expectOne('/api/seasons/4/matches/scheduled').flush([
      { seasonMatchID: 1, homeCaptain: 'H', awayCaptain: 'A', homeTeam: 'Home', awayTeam: 'Away', complete: false, time: '2026-01-07T19:00:00Z' },
    ]);
    await TestBed.inject(ApplicationRef).whenStable();
    expect(store.matches()[0].time.toUTC().toISO()).toBe('2026-01-07T19:00:00.000Z');

    seasonID.set(5);
    TestBed.tick();
    http.expectOne('/api/seasons/5/matches/scheduled').flush('x', { status: 500, statusText: 'Server Error' });
    await TestBed.inject(ApplicationRef).whenStable();
    expect(store.matches()).toEqual([]);
  });

  describe('live', () => {
    // The clock goes in once the first load is in (whenStable waits on the real one)
    beforeEach(async () => {
      store.seasonID.use(signal(4));
      TestBed.tick();
      const read = http.expectOne('/api/seasons/4/matches/scheduled');
      expect(read.request.method).toBe('GET');
      read.flush([{ seasonMatchID: 1, homeCaptain: 'H', awayCaptain: 'A', homeTeam: 'Home', awayTeam: 'Away', complete: false, time: '2026-01-07T19:00:00Z' }]);
      await TestBed.inject(ApplicationRef).whenStable();
      jasmine.clock().install();
      // No spread: a reload runs LIVE_RELOAD_MS after a change
      spyOn(Math, 'random').and.returnValue(0);
    });
    afterEach(() => jasmine.clock().uninstall());

    it('shows what is pushed with a change to the season, with no request', () => {
      expect(live.active).toEqual([['season:4']]);
      live.send({ topic: 'season:4', data: seasonLive(4, { scheduled: [{ seasonMatchID: 2, homeCaptain: 'H', awayCaptain: 'A', homeTeam: 'Home', awayTeam: 'Away', complete: true, time: '2026-01-14T19:00:00Z' }] }) });
      expect(store.matches().map(m => [m.seasonMatchID, m.time.toUTC().toISO()])).toEqual([[2, '2026-01-14T19:00:00.000Z']]);
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      TestBed.tick();
      http.expectNone('/api/seasons/4/matches/scheduled');
    });

    it("ignores another season's data", () => {
      live.send({ topic: 'season:4', data: seasonLive(5, { scheduled: [{ seasonMatchID: 2, homeCaptain: 'H', awayCaptain: 'A', homeTeam: 'Home', awayTeam: 'Away', complete: true, time: '2026-01-14T19:00:00Z' }] }) });
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      TestBed.tick();
      expect(store.matches().map(m => m.seasonMatchID)).toEqual([1]);
      http.expectNone(() => true);
    });

    it('reads it again on a change without data', () => {
      live.send({ topic: 'season:4' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      TestBed.tick();
      http.expectOne('/api/seasons/4/matches/scheduled').flush([{ seasonMatchID: 1, homeCaptain: 'H', awayCaptain: 'A', homeTeam: 'Home', awayTeam: 'Away', complete: false, time: '2026-01-07T19:00:00Z' }]);
    });
  });
});
