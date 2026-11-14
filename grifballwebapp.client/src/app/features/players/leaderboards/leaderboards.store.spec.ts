import { provideHttpClient, withXhr } from '@angular/common/http';
import type { HttpRequest } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { LeaderboardsDto, PlayerAllTimeRankDto } from '@api/models';
import { LeaderboardsStore } from './leaderboards.store';

const boards = { ringLeaders: [], dynasties: [], goalLeaders: [], killLeaders: [], gamesPlayedLeaders: [], carrierStopLeaders: [] } as LeaderboardsDto;
const rank = (gamertag: string) => ({ gamertag }) as PlayerAllTimeRankDto;

describe('LeaderboardsStore', () => {
  let store: LeaderboardsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [LeaderboardsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(LeaderboardsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const formats = (r: HttpRequest<unknown>) => r.params.getAll('formats');
  const boardsRequest = () => http.expectOne(r => r.url === '/api/leaderboards');
  const rankRequest = () => http.expectOne(r => r.url === '/api/leaderboards/player-rank');

  it('loads the boards for every format, naming none, and is loaded even when they fail', () => {
    store.load();
    const all = boardsRequest();
    expect(formats(all)).toBeNull();
    all.flush(boards);
    expect(store.boards()).toEqual(boards);
    expect(store.loaded()).toBeTrue();

    store.load();
    boardsRequest().flush('down', { status: 500, statusText: 'Server Error' });
    expect(store.boards()).toEqual(boards);
  });

  it('loads the formats chosen, in order, cancelling the load before; none chosen is every format again', () => {
    store.setFormats(['LateLeague', 'LeagueSeason']);
    const first = boardsRequest();
    expect(store.formats()).toEqual(['LeagueSeason', 'LateLeague']);
    store.setFormats([]);
    expect(first.cancelled).toBeTrue();
    expect(formats(boardsRequest())).toBeNull();
  });

  it("shows a player's standing, by user ID when picked, and looks them up again for new formats", () => {
    store.lookUp({ query: 'Legendary', userId: 1 });
    expect(store.spotlightLoading()).toBeTrue();
    const ask = rankRequest();
    expect(ask.request.params.get('userID')).toBe('1');
    expect(ask.request.params.get('query')).toBe('Legendary');
    ask.flush(rank('Legendary'));
    expect(store.spotlightLoading()).toBeFalse();
    expect(store.spotlight()!.gamertag).toBe('Legendary');

    store.setFormats(['LeagueSeason']);
    boardsRequest().flush(boards);
    expect(formats(rankRequest())).toEqual(['LeagueSeason']);
  });

  it('says when no player matches, and when the standing fails to load', () => {
    store.lookUp({ query: 'Nobody', userId: null });
    rankRequest().flush(null, { status: 404, statusText: 'Not Found' });
    expect(store.spotlight()).toBeNull();
    expect(store.spotlightError()).toBe('No player found matching "Nobody".');

    store.lookUp({ query: 'Legendary', userId: null });
    expect(store.spotlightError()).toBeNull();
    rankRequest().flush('down', { status: 500, statusText: 'Server Error' });
    expect(store.spotlightError()).toBe('Failed to load ranking for "Legendary".');
  });

  it('closes the standing, cancelling one still loading, and looks no one up for new formats', () => {
    store.lookUp({ query: 'Legendary', userId: null });
    const pending = rankRequest();
    store.lookUp(null);
    expect(pending.cancelled).toBeTrue();
    expect(store.spotlight()).toBeNull();
    expect(store.spotlightLoading()).toBeFalse();

    store.setFormats(['LeagueSeason']);
    boardsRequest().flush(boards);
    http.expectNone(r => r.url === '/api/leaderboards/player-rank');
  });
});
