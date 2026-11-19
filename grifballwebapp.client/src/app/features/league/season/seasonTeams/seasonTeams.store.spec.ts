import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { SeasonSummaryDto, SeasonTeamDto } from '@api/models';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS } from '@core/live/liveReload';
import { SeasonTeamsStore } from './seasonTeams.store';

const team = (teamName: string) => ({ teamName, players: [] }) as unknown as SeasonTeamDto;
const summary = { isHistorical: true } as SeasonSummaryDto;

/** The live service as the store sees it: who watches what, and a message to send them. */
class FakeLive {
  readonly watching: { topics: readonly string[], listener: (message: LiveMessage) => void }[] = [];

  watch(topics: readonly string[], listener: (message: LiveMessage) => void): () => void {
    this.watching.push({ topics, listener });
    return () => undefined;
  }

  send(message: LiveMessage): void {
    for (const watch of this.watching.filter(w => message.topic !== null && w.topics.includes(message.topic)))
      watch.listener(message);
  }
}

describe('SeasonTeamsStore', () => {
  let store: SeasonTeamsStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({ providers: [SeasonTeamsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }] });
    store = TestBed.inject(SeasonTeamsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads the season's teams and summary", () => {
    store.show(4);
    expect(store.seasonID()).toBe(4);
    expect(store.loading()).toBeTrue();
    http.expectOne('/api/seasons/4/summary').flush(summary);
    http.expectOne('/api/seasons/4/teams').flush([team('Alpha')]);
    expect(store.loading()).toBeFalse();
    expect(store.summary()).toEqual(summary);
    expect(store.teams().map(t => t.teamName)).toEqual(['Alpha']);
  });

  it('shows the teams without the summary when it fails', () => {
    store.show(4);
    http.expectOne('/api/seasons/4/summary').flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne('/api/seasons/4/teams').flush([team('Alpha')]);
    expect(store.summary()).toBeNull();
    expect(store.teams()).toHaveSize(1);
    expect(store.error()).toBeNull();
  });

  it('tells a season not found from a failed load', () => {
    store.show(999);
    http.expectOne('/api/seasons/999/summary').flush('x', { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/seasons/999/teams').flush('x', { status: 404, statusText: 'Not Found' });
    expect(store.error()).toBe('Season not found');
    expect(store.loading()).toBeFalse();

    store.show(5);
    expect(store.error()).toBeNull();
    http.expectOne('/api/seasons/5/summary').flush(summary);
    http.expectOne('/api/seasons/5/teams').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.error()).toBe('Failed to load season teams');
  });

  it("forgets the last season, and drops its answers, for a new one", () => {
    store.show(4);
    const oldTeams = http.expectOne('/api/seasons/4/teams');
    http.expectOne('/api/seasons/4/summary').flush(summary);
    store.show(5);
    expect(oldTeams.cancelled).toBeTrue();
    expect(store.summary()).toBeNull();
    http.expectOne('/api/seasons/5/summary').flush(summary);
    http.expectOne('/api/seasons/5/teams').flush([team('Beta')]);
    expect(store.teams().map(t => t.teamName)).toEqual(['Beta']);
  });

  it('watches no teams page for no season', () => {
    store.show(0);
    TestBed.tick();
    http.expectOne('/api/seasons/0/summary').flush(summary);
    http.expectOne('/api/seasons/0/teams').flush([]);
    expect(live.watching).toEqual([]);
  });

  describe('live', () => {
    const newer = { isHistorical: false } as SeasonSummaryDto;

    beforeEach(() => {
      store.show(4);
      TestBed.tick();
      http.expectOne('/api/seasons/4/summary').flush(summary);
      http.expectOne('/api/seasons/4/teams').flush([team('Alpha')]);
    });

    it("watches its season's teams page, and shows the teams and banner pushed with a change, with no request", () => {
      expect(live.watching.map(w => w.topics)).toEqual([['teams:4']]);
      live.send({ topic: 'teams:4', data: { seasonID: 4, summary: newer, teams: [team('Alpha'), team('Beta')] } });
      expect(store.teams().map(t => t.teamName)).toEqual(['Alpha', 'Beta']);
      expect(store.summary()).toEqual(newer);
      live.send({ topic: 'teams:4', data: { seasonID: 4, summary: null, teams: [] } });
      expect(store.summary()).toBeNull();
      http.expectNone(() => true);
    });

    it("ignores another season's, and reads a season that is gone, to say so", () => {
      jasmine.clock().install();
      spyOn(Math, 'random').and.returnValue(0);
      try {
        live.send({ topic: 'teams:4', data: { seasonID: 5, summary: newer, teams: [team('Other')] } });
        expect(store.teams().map(t => t.teamName)).toEqual(['Alpha']);
        live.send({ topic: 'teams:4', data: { seasonID: 4, summary: null, teams: null } });
        jasmine.clock().tick(LIVE_RELOAD_MS);
      } finally {
        jasmine.clock().uninstall();
      }
      http.expectOne('/api/seasons/4/summary').flush('x', { status: 404, statusText: 'Not Found' });
      http.expectOne('/api/seasons/4/teams').flush('x', { status: 404, statusText: 'Not Found' });
      expect(store.error()).toBe('Season not found');
    });
  });
});
