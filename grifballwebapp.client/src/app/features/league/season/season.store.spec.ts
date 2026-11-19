import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { SeasonLiveDto, SeasonsLiveDto, SeasonSummaryDto } from '@api/models';
import { SeasonStore } from './season.store';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS } from '@core/live/liveReload';

const summary = (seasonID: number) => ({ seasonID, seasonName: `Season ${seasonID}` }) as SeasonSummaryDto;
/** A season's page as the live hub pushes it: nothing but what a test gives. */
const seasonLive = (seasonID: number, overrides: Partial<SeasonLiveDto> = {}): SeasonLiveDto =>
  ({ seasonID, summary: null, standings: [], scheduled: [], unscheduled: [], playerStats: [], ...overrides });

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

describe('SeasonStore', () => {
  let store: SeasonStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [SeasonStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }],
    });
    store = TestBed.inject(SeasonStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.seasonName()).toBe('Season');
  });

  it("loads the route's season's summary, and its name with it", () => {
    store.seasonID.use(signal(4));
    TestBed.tick();
    expect(store.shownSeasonID()).toBe(4);
    expect(store.nameLoaded()).toBeFalse();
    http.expectOne('/api/seasons/4/summary').flush(summary(4));
    expect(store.summary()).toEqual(summary(4));
    expect(store.seasonName()).toBe('Season 4');
    expect(store.nameLoaded()).toBeTrue();
  });

  it('shows the current season when the route names none, and the name is in even when the summary fails', () => {
    store.seasonID.use(signal(0));
    TestBed.tick();
    http.expectOne('/api/seasons/current').flush(12);
    expect(store.shownSeasonID()).toBe(12);
    http.expectOne('/api/seasons/12/summary').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.summary()).toBeNull();
    expect(store.nameLoaded()).toBeTrue();
  });

  it("forgets the last season, and drops its late answers, for another", () => {
    const seasonID = signal(0);
    store.seasonID.use(seasonID);
    TestBed.tick();
    const current = http.expectOne('/api/seasons/current');
    seasonID.set(4);
    TestBed.tick();
    const late = http.expectOne('/api/seasons/4/summary');
    current.flush(12);
    expect(store.shownSeasonID()).toBe(4);

    seasonID.set(5);
    TestBed.tick();
    late.flush(summary(4));
    expect(store.summary()).toBeNull();
    expect(store.seasonName()).toBe('Season');
    http.expectOne('/api/seasons/5/summary').flush(summary(5));
    expect(store.seasonName()).toBe('Season 5');
  });

  it('loads the summary again on request', () => {
    store.seasonID.use(signal(4));
    TestBed.tick();
    http.expectOne('/api/seasons/4/summary').flush(summary(4));
    store.loadSummary();
    http.expectOne('/api/seasons/4/summary').flush({ ...summary(4), seasonName: 'Renamed' });
    expect(store.seasonName()).toBe('Renamed');
  });

  it('loads the events, and is done with them even when they fail', () => {
    store.loadEvents();
    http.expectOne('/api/events').flush([{ seasonID: 4 }]);
    expect(store.events()).toHaveSize(1);
    expect(store.eventsLoaded()).toBeTrue();

    store.loadEvents();
    http.expectOne('/api/events').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.eventsLoaded()).toBeTrue();
  });

  describe('live', () => {
    beforeEach(() => {
      jasmine.clock().install();
      // No spread: a reload runs LIVE_RELOAD_MS after a change
      spyOn(Math, 'random').and.returnValue(0);
      store.seasonID.use(signal(4));
      TestBed.tick();
      http.expectOne('/api/seasons/4/summary').flush(summary(4));
    });
    afterEach(() => jasmine.clock().uninstall());

    /** Long enough for any reload a change could start. */
    const waitOut = () => jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);

    it('watches the season shown', () => {
      expect(live.active).toEqual([['season:4']]);
    });

    it('shows the summary pushed with a change to the season, with no request', () => {
      live.send({ topic: 'season:4', data: seasonLive(4, { summary: { ...summary(4), seasonName: 'Pushed' } }) });
      expect(store.seasonName()).toBe('Pushed');
      waitOut();
      http.expectNone('/api/seasons/4/summary');
    });

    it("ignores another season's data", () => {
      live.send({ topic: 'season:4', data: seasonLive(5, { summary: { ...summary(5), seasonName: 'Other' } }) });
      waitOut();
      expect(store.seasonName()).toBe('Season 4');
      http.expectNone(() => true);
    });

    it('loads the summary when the data comes without one, or a change comes without data', () => {
      live.send({ topic: 'season:4', data: seasonLive(4) });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectOne('/api/seasons/4/summary').flush({ ...summary(4), seasonName: 'Read' });
      expect(store.seasonName()).toBe('Read');

      live.send({ topic: 'season:4' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectOne('/api/seasons/4/summary').flush(summary(4));
    });

    it('watches the seasons once its events are in, and shows the events pushed with a change, with no request', () => {
      expect(live.active).not.toContain(['seasons']);
      store.loadEvents();
      http.expectOne('/api/events').flush([]);
      TestBed.tick();
      expect(live.active).toContain(['seasons']);

      const pushed: SeasonsLiveDto = { events: [{ seasonID: 7, name: 'Winter', eventType: 'Season', start: '2026-11-20T00:00:00Z', end: '2027-02-15T00:00:00Z' }] };
      live.send({ topic: 'seasons', data: pushed });
      expect(store.events()).toEqual(pushed.events);
      waitOut();
      http.expectNone('/api/events');

      live.send({ topic: 'seasons' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectOne('/api/events').flush([]);
      expect(store.events()).toEqual([]);
    });
  });
});
