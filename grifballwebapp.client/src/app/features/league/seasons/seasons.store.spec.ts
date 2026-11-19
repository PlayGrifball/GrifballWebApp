import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { SeasonListItemDto } from '@api/models';
import { SEASONS_PAGE_SIZE, SeasonsStore } from './seasons.store';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS } from '@core/live/liveReload';

const season = (seasonID: number, leagueID: number, year: number, gameVersionID: number | null) =>
  ({ seasonID, leagueID, leagueName: `League ${leagueID}`, year, gameVersionID, gameVersionName: gameVersionID ? `Game ${gameVersionID}` : null }) as SeasonListItemDto;
const page = (...results: SeasonListItemDto[]) => ({ results });
const all = { leagueID: null, gameVersionID: null, format: null, year: null };

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

describe('SeasonsStore', () => {
  let store: SeasonsStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [SeasonsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }],
    });
    store = TestBed.inject(SeasonsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const list = () => http.expectOne(r => r.url === '/api/seasons');

  it('loads every season, with the choices from them', () => {
    store.load(all, true);
    const request = list();
    expect(request.request.params.get('pageSize')).toBe(String(SEASONS_PAGE_SIZE));
    expect(request.request.params.has('leagueID')).toBeFalse();
    request.flush(page(season(1, 2, 2024, 6), season(2, 1, 2026, null), season(3, 1, 2024, 6)));
    expect(store.loaded()).toBeTrue();
    expect(store.seasons()).toHaveSize(3);
    expect(store.leagues()).toEqual([{ id: 1, name: 'League 1' }, { id: 2, name: 'League 2' }]);
    expect(store.games()).toEqual([{ id: 6, name: 'Game 6' }]);
    expect(store.years()).toEqual([2026, 2024]);
  });

  it('loads the seasons a filter picks, leaving the choices, and drops the last filter\'s list', () => {
    store.load({ leagueID: 1, gameVersionID: 6, format: 'Tournament', year: 2024 }, false);
    const first = list();
    expect(first.request.params.get('leagueID')).toBe('1');
    expect(first.request.params.get('gameVersionID')).toBe('6');
    expect(first.request.params.get('format')).toBe('Tournament');
    expect(first.request.params.get('year')).toBe('2024');

    store.load({ leagueID: 2, gameVersionID: null }, false);
    expect(first.cancelled).toBeTrue();
    expect(store.loaded()).toBeFalse();
    list().flush(page(season(1, 2, 2024, 6)));
    expect(store.seasons()).toHaveSize(1);
    expect(store.leagues()).toEqual([]);
  });

  it('is loaded with what it had when the list fails', () => {
    store.load(all, false);
    list().flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeTrue();
    expect(store.seasons()).toEqual([]);
  });

  it('loads the choices from every season', () => {
    store.loadOptions();
    const request = list();
    expect(request.request.params.keys()).toEqual(['pageSize']);
    request.flush(page(season(1, 2, 2024, 6)));
    expect(store.leagues()).toEqual([{ id: 2, name: 'League 2' }]);
    expect(store.seasons()).toEqual([]);
  });

  describe('live', () => {
    beforeEach(() => {
      jasmine.clock().install();
      // No spread: a reload runs LIVE_RELOAD_MS after a change
      spyOn(Math, 'random').and.returnValue(0);
      TestBed.tick();
    });
    afterEach(() => jasmine.clock().uninstall());

    it('loads nothing on a change to the seasons before a list is shown', () => {
      expect(live.active).toEqual([['seasons']]);
      live.send({ topic: 'seasons' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectNone(() => true);
    });

    it('loads the list shown again, in place, with its filter and its choices', () => {
      store.load({ leagueID: 1, gameVersionID: null }, true);
      list().flush(page(season(1, 1, 2024, 6)));

      live.send({ topic: 'seasons' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      expect(store.loaded()).toBeTrue();
      const again = list();
      expect(again.request.params.get('leagueID')).toBe('1');
      again.flush(page(season(1, 1, 2024, 6), season(2, 2, 2026, null)));
      expect(store.seasons()).toHaveSize(2);
      expect(store.years()).toEqual([2026, 2024]);
    });
  });
});
