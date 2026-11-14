import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { OnDemandJobStatusDto } from '@api/models';
import { MatchAction, SeasonMatchStore } from './seasonMatch.store';
import { SeasonMatchPageDto } from './seasonMatchPageDto';

const idlePull: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };

const page = (overrides: Partial<SeasonMatchPageDto> = {}): SeasonMatchPageDto => ({
  seasonID: 3,
  seasonName: 'Season 3',
  isPlayoff: false,
  homeTeamName: 'Home',
  homeTeamID: 10,
  homeTeamScore: null,
  homeTeamResult: 'TBD',
  awayTeamName: 'Away',
  awayTeamID: 20,
  awayTeamScore: null,
  awayTeamResult: 'TBD',
  scheduledTime: null,
  bestOf: 3,
  reportedGames: [],
  bracketInfo: null,
  activeRescheduleRequestId: null,
  canRequestReschedule: false,
  decidedBy: null,
  canUndo: false,
  ...overrides,
});

const team = (name: string) => ({ teamName: name, players: [{ teamPlayerID: 1, userID: 1, gamertag: `${name} captain`, draftCaptainOrder: 1, draftRound: null }] });

describe('SeasonMatchStore', () => {
  let store: SeasonMatchStore;
  let http: HttpTestingController;

  const setUp = (platform = 'browser') => {
    TestBed.configureTestingModule({
      providers: [SeasonMatchStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: PLATFORM_ID, useValue: platform }],
    });
    store = TestBed.inject(SeasonMatchStore);
    http = TestBed.inject(HttpTestingController);
  };

  /** Opens match 7 and answers its three requests: the page, its possible matches and its pull. */
  const open = (dto: SeasonMatchPageDto) => {
    store.open(7);
    http.expectOne('/api/season-matches/7').flush(dto);
    http.expectOne('/api/season-matches/7/possible-matches').flush([]);
    http.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
    TestBed.tick();
  };

  afterEach(() => http.verify());

  it('loads a match, then both rosters, and is ready once all three are in', () => {
    setUp();
    open(page());
    expect(store.seasonMatch()!.homeTeamName).toBe('Home');
    expect(store.pageLoaded()).toBeTrue();
    expect(store.ready()).toBeFalse();

    http.expectOne('/api/teams/10').flush(team('Home'));
    expect(store.ready()).toBeFalse();
    http.expectOne('/api/teams/20').flush(team('Away'));
    expect(store.ready()).toBeTrue();
    expect(store.homeRoster().map(p => p.gamertag)).toEqual(['Home captain']);
    expect(store.awayRoster().map(p => p.gamertag)).toEqual(['Away captain']);
  });

  it('drops the previous match when another opens, and cancels its request', () => {
    setUp();
    open(page());
    http.match(/\/api\/teams\//).forEach(r => r.flush(team('x')));

    const first = store.open(8);
    expect(store.seasonMatch()).toBeNull();
    const request = http.expectOne('/api/season-matches/8');
    http.expectOne('/api/season-matches/8/possible-matches').flush([]);
    http.expectOne('/api/season-matches/8/match-pull').flush(idlePull);
    first.unsubscribe();
    expect(request.cancelled).toBeTrue();
  });

  it('keeps the rosters when the match reloads with the same teams, and has none for a BYE', () => {
    setUp();
    open(page());
    http.expectOne('/api/teams/10').flush(team('Home'));
    http.expectOne('/api/teams/20').flush(team('Away'));

    store.reload();
    http.expectOne('/api/season-matches/7').flush(page({ homeTeamScore: 1 }));
    TestBed.tick();
    http.expectNone(/\/api\/teams\//);
    expect(store.seasonMatch()!.homeTeamScore).toBe(1);

    store.reload();
    http.expectOne('/api/season-matches/7').flush(page({ awayTeamID: null, awayTeamName: null }));
    TestBed.tick();
    expect(store.awayRoster()).toEqual([]);
    expect(store.ready()).toBeTrue();
  });

  it('sends each commissioner action to its endpoint', () => {
    setUp();
    open(page());
    http.match(/\/api\/teams\//).forEach(r => r.flush(team('x')));
    const endpoints: Record<MatchAction, string> = {
      HomeForfeit: '/api/season-matches/7/forfeit/home',
      AwayForfeit: '/api/season-matches/7/forfeit/away',
      DoubleForfeit: '/api/season-matches/7/forfeit/double',
      UndoResult: '/api/season-matches/7/result/undo',
    };
    for (const [action, url] of Object.entries(endpoints)) {
      store.act(action as MatchAction).subscribe();
      http.expectOne({ method: 'POST', url }).flush(null);
    }
  });

  it('reports a Waypoint game as a new game, or in place of one', () => {
    setUp();
    open(page());
    http.match(/\/api\/teams\//).forEach(r => r.flush(team('x')));

    store.report('abc').subscribe();
    expect(http.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc/report' }).request.params.has('replaces')).toBeFalse();
    store.report('def', 2).subscribe();
    expect(http.expectOne(r => r.url === '/api/season-matches/7/games/def/report').request.params.get('replaces')).toBe('2');
  });

  it('asks for neither the possible matches nor the pull on the server', () => {
    setUp('server');
    store.open(7);
    http.expectOne('/api/season-matches/7').flush(page());
    http.expectNone('/api/season-matches/7/possible-matches');
    http.expectNone('/api/season-matches/7/match-pull');
    expect(store.possibleMatchesState()).toBe('loading');
    expect(store.pull()).toBeNull();
    TestBed.tick();
    http.match(/\/api\/teams\//).forEach(r => r.flush(team('x')));
  });
});
