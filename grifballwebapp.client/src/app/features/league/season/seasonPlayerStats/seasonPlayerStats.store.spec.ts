import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { SeasonLiveDto, SeasonPlayerStatDto, TeamLiveDto } from '@api/models';
import { SeasonPlayerStatsStore } from './seasonPlayerStats.store';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS } from '@core/live/liveReload';

const row = (gamertag: string) => ({ gamertag }) as SeasonPlayerStatDto;

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

describe('SeasonPlayerStatsStore', () => {
  let store: SeasonPlayerStatsStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [SeasonPlayerStatsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }],
    });
    store = TestBed.inject(SeasonPlayerStatsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const request = (seasonID: number) => http.expectOne(r => r.url === `/api/seasons/${seasonID}/player-stats`);

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.stats()).toEqual([]);
  });

  it("loads the season's stats, then one team's, starting over for each", async () => {
    const teamID = signal<number | null>(null);
    store.seasonID.use(signal(4));
    store.teamID.use(teamID);
    TestBed.tick();
    const season = request(4);
    expect(season.request.params.has('teamID')).toBeFalse();
    expect(store.loaded()).toBeFalse();
    season.flush([row('Grif')]);
    await settle();
    expect(store.stats().map(r => r.gamertag)).toEqual(['Grif']);
    expect(store.loaded()).toBeTrue();

    teamID.set(10);
    TestBed.tick();
    expect(store.stats()).toEqual([]);
    const team = request(4);
    expect(team.request.params.get('teamID')).toBe('10');
    team.flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.stats()).toEqual([]);
    expect(store.loaded()).toBeTrue();
  });

  describe('live', () => {
    /** A season's page as the live hub pushes it: only its players' stats. */
    const seasonLive = (seasonID: number, playerStats: SeasonPlayerStatDto[]): SeasonLiveDto =>
      ({ seasonID, summary: null, standings: [], scheduled: [], unscheduled: [], playerStats });
    /** A team's page as the live hub pushes it: only its players' stats. */
    const teamLive = (teamID: number, playerStats: SeasonPlayerStatDto[]): TeamLiveDto =>
      ({ teamID, team: null, matches: [], playerStats });
    const teamID = signal<number | null>(null);

    // The clock goes in once the first load is in (whenStable waits on the real one), and out before the next
    beforeEach(async () => {
      teamID.set(null);
      store.seasonID.use(signal(4));
      store.teamID.use(teamID);
      TestBed.tick();
      request(4).flush([row('Grif')]);
      await settle();
      jasmine.clock().install();
      // No spread: a reload runs LIVE_RELOAD_MS after a change
      spyOn(Math, 'random').and.returnValue(0);
    });
    afterEach(() => jasmine.clock().uninstall());

    it("shows the season's stats pushed with a change, with no request", () => {
      expect(live.active).toEqual([['season:4']]);
      live.send({ topic: 'season:4', data: seasonLive(4, [row('Simmons'), row('Grif')]) });
      expect(store.stats().map(r => r.gamertag)).toEqual(['Simmons', 'Grif']);
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      http.expectNone(r => r.url === '/api/seasons/4/player-stats');
    });

    it("ignores another season's stats", () => {
      live.send({ topic: 'season:4', data: seasonLive(5, [row('Other')]) });
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      expect(store.stats().map(r => r.gamertag)).toEqual(['Grif']);
      http.expectNone(() => true);
    });

    /** Shows team 10's stats (its page's table), its first read in. */
    const showTeam = async () => {
      jasmine.clock().uninstall();
      teamID.set(10);
      TestBed.tick();
      request(4).flush([row('Grif')]);
      await settle();
      jasmine.clock().install();
    };

    it("watches a team's page for its stats, and shows them pushed with a change, not another team's", async () => {
      await showTeam();

      expect(live.active).toEqual([['team:10']]);
      live.send({ topic: 'team:10', data: teamLive(11, [row('Other')]) });
      expect(store.stats().map(r => r.gamertag)).toEqual(['Grif']);
      live.send({ topic: 'team:10', data: teamLive(10, [row('Grif'), row('Sarge')]) });
      expect(store.stats().map(r => r.gamertag)).toEqual(['Grif', 'Sarge']);
      jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
      http.expectNone(() => true);
    });

    it("ignores a season page's stats while it shows a team's", async () => {
      await showTeam();

      live.send({ topic: 'team:10', data: seasonLive(4, [row('Simmons')]) });
      expect(store.stats().map(r => r.gamertag)).toEqual(['Grif']);
    });

    it('reads the stats again on a change without data', () => {
      live.send({ topic: 'season:4' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      TestBed.tick();
      const read = request(4);
      expect(read.request.method).toBe('GET');
      read.flush([row('Grif')]);
    });
  });
});
