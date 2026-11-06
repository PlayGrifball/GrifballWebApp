import { DateTime } from 'luxon';
import { DatePipe } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PULL_POLL_MS, SeasonMatchComponent } from './seasonMatch.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { Component, input, output, PLATFORM_ID, signal } from '@angular/core';
import { RecordedGamesComponent } from './recordedGames/recordedGames.component';
import { AccountService } from '../../account.service';
import { BracketInfoDto, SeasonMatchPageDto } from './seasonMatchPageDto';
import { PossibleMatchDto, PossiblePlayerDto } from './possibleMatchDto';
import { VodListComponent } from '../../shared/vodList/vodList.component';
import { StubVodListComponent } from '../../shared/vodList/vodList.stub';
import type { OnDemandJobStatusDto } from '../../api/dtos/onDemandJobDto';

/** The recorded games are tested on their own (recordedGames.component.spec.ts): here a stub, which asks for nothing. */
@Component({ selector: 'app-recorded-games', template: '' })
class StubRecordedGamesComponent {
  readonly seasonMatchID = input<number>();
  readonly match = input<SeasonMatchPageDto>();
  readonly homeRoster = input<unknown[]>();
  readonly awayRoster = input<unknown[]>();
  readonly canEdit = input<boolean>();
  readonly changed = output<void>();
}

/** The VOD list and the recorded games are tested on their own: here stubs, so the page makes no request for them. */
const withoutVodList = { remove: { imports: [VodListComponent, RecordedGamesComponent] }, add: { imports: [StubVodListComponent, StubRecordedGamesComponent] } };
/** No pull queued or running, and none in the history kept. */
const idlePull: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };

describe('SeasonMatchComponent', () => {
  let component: SeasonMatchComponent;
  let fixture: ComponentFixture<SeasonMatchComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            parent: {
              params: of({ id: 'test-season-id' })
            }
          }
        }
      ]
    }).overrideComponent(SeasonMatchComponent, withoutVodList).compileComponents();

    fixture = TestBed.createComponent(SeasonMatchComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

// Angular 22 changed `a?.b` in templates to evaluate to undefined (not null) when `a` is
// nullish. These tests pin the rendering of the template's `?.` comparisons both before
// the page DTO has loaded (seasonMatch() is null) and after.
describe('SeasonMatchComponent template (safe navigation)', () => {
  let fixture: ComponentFixture<SeasonMatchComponent>;
  let httpMock: HttpTestingController;

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
    ...overrides
  });

  const bracket = (overrides: Partial<BracketInfoDto> = {}): BracketInfoDto => ({
    homeTeamSeed: 1,
    awayTeamSeed: 4,
    homeTeamPreviousMatchID: null,
    awayTeamPreviousMatchID: null,
    winnerNextMatchID: null,
    loserNextMatchID: null,
    isGrandFinal: false,
    ...overrides
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AccountService,
          useValue: { isEventOrganizer: signal(false), isPlayer: signal(false) }
        }
      ]
    }).overrideComponent(SeasonMatchComponent, withoutVodList).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonMatchComponent);
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();
  });

  function load(dto: SeasonMatchPageDto): void {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(dto);
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function links(): { text: string, href: string | null }[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a.block'))
      .map(a => ({ text: a.textContent?.trim() ?? '', href: a.getAttribute('href') }));
  }

  it('lists the match\'s VODs', () => {
    const vods = fixture.debugElement.query(By.directive(StubVodListComponent)).componentInstance as StubVodListComponent;
    expect(vods.scope()).toBe('seasonMatch');
    expect(vods.id()).toBe(7);
  });

  it('shows the regular season text, no bracket links and no date before the page loads', () => {
    expect(text()).toContain('Regular Season Match');
    expect(links()).toEqual([]);
    expect(text()).toContain('Not scheduled');
    expect(text()).not.toContain('Starts');
  });

  it('shows the empty page for an unknown match (the API answers 204 with no body)', () => {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(null, { status: 204, statusText: 'No Content' });
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    expect(() => fixture.detectChanges()).not.toThrow();

    expect(fixture.componentInstance.seasonMatch()).toBeNull();
    expect(text()).toContain('Regular Season Match');
  });

  it('shows the regular season text when the match has no bracket info', () => {
    load(page());

    expect(text()).toContain('Regular Season Match');
    expect(links()).toEqual([]);
  });

  function teamTiles(): { text: string, linked: boolean }[] {
    return fixture.debugElement.queryAll(By.css('.lobby-team__banner'))
      .map(d => ({ text: (d.nativeElement as HTMLElement).querySelector('span:not(.hud-label)')?.textContent?.trim() ?? '', linked: d.injector.get(RouterLink, null, { self: true }) !== null }))
      .filter(t => t.text === 'Home' || t.text === 'Away' || t.text === 'BYE');
  }

  it('links both teams for a match with two teams', () => {
    load(page());

    expect(teamTiles()).toEqual([{ text: 'Home', linked: true }, { text: 'Away', linked: true }]);
  });

  it('shows an away BYE without a team link', () => {
    load(page({ isPlayoff: true, homeTeamResult: 'Won', awayTeamName: null, awayTeamID: null, awayTeamResult: 'Bye' }));

    expect(teamTiles()).toEqual([{ text: 'Home', linked: true }, { text: 'BYE', linked: false }]);
  });

  it('shows a home BYE without a team link', () => {
    load(page({ isPlayoff: true, homeTeamName: null, homeTeamID: null, homeTeamResult: 'Bye', awayTeamResult: 'Won' }));

    expect(teamTiles()).toEqual([{ text: 'BYE', linked: false }, { text: 'Away', linked: true }]);
  });

  it('shows seeds and next-match links for a first-round playoff match', () => {
    load(page({ isPlayoff: true, bracketInfo: bracket({ winnerNextMatchID: 11, loserNextMatchID: 12 }) }));

    expect(text()).not.toContain('Regular Season Match');
    expect(text()).toContain('Home Team Seed 1');
    expect(text()).toContain('Away Team Seed 4');
    expect(links()).toEqual([
      { text: 'Winner Next Match', href: '/seasonmatch/11' },
      { text: 'Loser Next Match', href: '/seasonmatch/12' }
    ]);
  });

  it('shows previous-match links and final-match text for a playoff final', () => {
    load(page({ isPlayoff: true, bracketInfo: bracket({ homeTeamPreviousMatchID: 5, awayTeamPreviousMatchID: 6 }) }));

    expect(text()).not.toContain('Home Team Seed');
    expect(text()).not.toContain('Away Team Seed');
    expect(text()).toContain('Winner wins playoffs');
    expect(text()).toContain('Loser is eliminated from playoffs');
    expect(links().map(l => l.text)).toEqual(['Home Team Previous Match', 'Away Team Previous Match']);
    expect(links()[0].href).toBe('/seasonmatch/5');
  });

  it("points each previous-match link at that side's own previous match", () => {
    load(page({ isPlayoff: true, bracketInfo: bracket({ homeTeamPreviousMatchID: 5, awayTeamPreviousMatchID: 6 }) }));

    expect(links()).toEqual([
      { text: 'Home Team Previous Match', href: '/seasonmatch/5' },
      { text: 'Away Team Previous Match', href: '/seasonmatch/6' }
    ]);
  });

  it('formats the scheduled time once loaded, as the date pipe shows the API value', () => {
    // As the API sends it: ISO 8601 in UTC.
    load(page({ scheduledTime: '2026-01-02T03:04:00Z' as unknown as SeasonMatchPageDto['scheduledTime'] }));

    expect(fixture.componentInstance.seasonMatch()!.scheduledTime instanceof DateTime).toBeTrue();
    expect(text()).toContain('Starts ' + new DatePipe('en-US').transform('2026-01-02T03:04:00Z', 'short'));
  });
});

describe('SeasonMatchComponent reporting', () => {
  let fixture: ComponentFixture<SeasonMatchComponent>;
  let component: SeasonMatchComponent;
  let httpMock: HttpTestingController;
  let isEventOrganizer: ReturnType<typeof signal<boolean>>;
  let isPlayer: ReturnType<typeof signal<boolean>>;

  const page = (overrides: Partial<SeasonMatchPageDto> = {}): SeasonMatchPageDto => ({
    seasonID: 3, seasonName: 'Season 3', isPlayoff: false,
    homeTeamName: 'Home', homeTeamID: 10, homeTeamScore: null, homeTeamResult: 'TBD',
    awayTeamName: 'Away', awayTeamID: 20, awayTeamScore: null, awayTeamResult: 'TBD',
    scheduledTime: null, bestOf: 3, reportedGames: [], bracketInfo: null, activeRescheduleRequestId: null, canRequestReschedule: false,
    ...overrides
  });

  const bracket = (overrides: Partial<BracketInfoDto> = {}): BracketInfoDto => ({
    homeTeamSeed: 1, awayTeamSeed: 4, homeTeamPreviousMatchID: null, awayTeamPreviousMatchID: null,
    winnerNextMatchID: 11, loserNextMatchID: 12, isGrandFinal: false,
    ...overrides
  });

  const player = (gamertag: string, score: number): PossiblePlayerDto =>
    ({ xboxUserID: score, gamertag, score, kills: score + 1, deaths: score + 2, isOnTeam: true });

  const possible: PossibleMatchDto = {
    matchID: 'abc-123',
    homeTeam: { teamID: 10, score: 50, outcome: 'Won', players: [player('Grif', 11)] },
    awayTeam: { teamID: 20, score: 49, outcome: 'Lost', players: [player('Tucker', 7)] },
  };

  beforeEach(async () => {
    isEventOrganizer = signal(true);
    isPlayer = signal(false);
    spyOn(console, 'log');
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { isEventOrganizer, isPlayer } }
      ]
    }).overrideComponent(SeasonMatchComponent, withoutVodList).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonMatchComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function load(dto: SeasonMatchPageDto, matches: PossibleMatchDto[] = []): void {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(dto);
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush(matches);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    fixture.detectChanges();
    // The rosters are not under test here.
    httpMock.match(r => r.url.startsWith('/api/Team/Team/')).forEach(r => r.flush(null));
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
    .find(b => b.textContent!.trim() === label) as HTMLButtonElement | undefined;

  it('lists reported games and scoreboards', () => {
    load(page({
      reportedGames: [
        {
          matchNumber: 1,
          matchID: 'g-1',
          homePlayers: [{ userID: 1, gamertag: 'Chief', kills: 20, deaths: 10, assists: 2, bombScores: 3, score: null }],
          awayPlayers: [{ userID: 2, gamertag: 'Arbiter', kills: 10, deaths: 20, assists: 1, bombScores: 1, score: null }]
        },
        { matchNumber: 2, matchID: 'g-2' }
      ]
    }));
    const tabs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.hud-tabs button'))
      .map(b => b.textContent!.trim());
    expect(tabs).toEqual(['Series Totals', 'Game 1', 'Game 2']);
    // A game's tab links its Waypoint ID to the game's match details page
    component.selectedGameTab.set(1);
    fixture.detectChanges();
    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('.lobby-game-meta a')).map(a => a.getAttribute('href'));
    expect(links).toEqual(['/match/g-1']);
    expect(text()).not.toContain('There are no reported games');
  });

  it('renders player box score stats and team totals', () => {
    load(page({
      reportedGames: [
        {
          matchNumber: 1,
          matchID: '00000000-0000-0000-0000-000000000001',
          homeScore: 5,
          awayScore: 2,
          homePlayers: [{ userID: 1, gamertag: 'Chief', kills: 25, deaths: 10, assists: 5, bombScores: 3, score: 300 }],
          awayPlayers: [{ userID: 2, gamertag: 'Arbiter', kills: 15, deaths: 25, assists: 2, bombScores: 2, score: 200 }]
        }
      ]
    }));
    fixture.detectChanges();

    const homeRow = (fixture.nativeElement as HTMLElement).querySelector('.scoreboard-team--home tbody tr')!;
    expect(homeRow.textContent).toContain('Chief');
    expect(homeRow.textContent).toContain('25'); // kills
    expect(homeRow.textContent).toContain('10'); // deaths
    expect(homeRow.textContent).toContain('3');  // bomb

    const totalsRow = (fixture.nativeElement as HTMLElement).querySelector('.scoreboard-team--home tfoot tr')!;
    expect(totalsRow.textContent).toContain('Team Total');
    expect(totalsRow.textContent).toContain('25');
  });

  it('has no scoreboard before any game is reported', () => {
    load(page());
    expect(component.activeScoreboard()).toBeNull();
  });

  it('shows the series totals on the series tab, with no players when a side has none', () => {
    load(page({
      homeTeamScore: 2, awayTeamScore: 1,
      reportedGames: [{ matchNumber: 1, matchID: 'g-1' }, { matchNumber: 2, matchID: 'g-2' }],
      seriesTotals: { homePlayers: [{ userID: 1, gamertag: 'Chief', kills: 40, deaths: 20, assists: 4, bombScores: 6, score: null }] } as SeasonMatchPageDto['seriesTotals']
    }));
    component.selectedGameTab.set('series');

    const board = component.activeScoreboard()!;
    expect(board.title).toBe('Series Totals');
    expect(board.isSeries).toBeTrue();
    expect([board.homeScore, board.awayScore]).toEqual([2, 1]);
    expect(board.homePlayers.map(p => p.gamertag)).toEqual(['Chief']);
    expect(board.awayPlayers).toEqual([]);
  });

  it('shows the first game on the series tab when there are no series totals', () => {
    load(page({ reportedGames: [{ matchNumber: 1, matchID: 'g-1', homeScore: 5, awayScore: 3 }] }));
    component.selectedGameTab.set('series');

    const board = component.activeScoreboard()!;
    expect(board.title).toBe('Game 1');
    expect(board.isSeries).toBeFalse();
    expect([board.homeScore, board.awayScore]).toEqual([5, 3]);
    expect(board.homePlayers).toEqual([]);
  });

  it('labels the sides home and away with their team names, whatever colour they played', () => {
    load(page({
      homeTeamName: 'Red Rampage', awayTeamName: 'Blue Bombers',
      reportedGames: [{ matchNumber: 1, matchID: 'g-1', homeScore: 5, awayScore: 2, homePlayers: [], awayPlayers: [] }],
    }));
    const header = (side: string) => Array.from((fixture.nativeElement as HTMLElement).querySelector(`.scoreboard-team--${side} .scoreboard-team-header`)!.children)
      .map(c => c.textContent!.trim()).join(' ');
    expect(header('home')).toBe('Home Red Rampage 5');
    expect(header('away')).toBe('Away Blue Bombers 2');
  });

  it('shows a row for every player, even two with the same gamertag', () => {
    const unknown = { userID: null, gamertag: 'Unknown Spartan', kills: 1, deaths: 1, assists: 0, bombScores: 0, score: null };
    load(page({
      reportedGames: [{ matchNumber: 1, matchID: 'g-1', homePlayers: [{ ...unknown, kills: 4 }, { ...unknown, kills: 2 }], awayPlayers: [] }],
    }));
    const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.scoreboard-team--home tbody tr'));
    expect(rows.map(r => r.children[1].textContent!.trim())).toEqual(['4', '2']);
  });

  it('hands the recorded games to their list, and reloads the match when one changes', () => {
    const recorded = {
      recordedGameID: 41, gameNumber: 1, gameVersionID: 1, gameVersionName: 'Halo 3', map: null, gametype: null, playedAt: null,
      homeTeamScore: 5, awayTeamScore: 2, homeTeamWon: true, awayTeamWon: false, isManual: true, players: [],
    };
    load(page({ recordedGames: [recorded] }));
    expect(text()).not.toContain('There are no reported games');
    expect(component.canUndo()).toBeTrue();
    const list = fixture.debugElement.query(By.directive(StubRecordedGamesComponent)).componentInstance as StubRecordedGamesComponent;
    expect(list.match()?.recordedGames).toEqual([recorded]);
    expect(list.canEdit()).toBeTrue();

    list.changed.emit();
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
  });

  it('shows an active reschedule request instead of the reschedule button', () => {
    load(page({ activeRescheduleRequestId: 88 }));
    expect(text()).toContain('Request ID: 88');
    expect(button('Reschedule')).toBeUndefined();
  });

  it('offers a reschedule link when the server says this user may request one', () => {
    isEventOrganizer.set(false);
    isPlayer.set(true);
    load(page({ canRequestReschedule: true }));
    expect(button('Reschedule')).toBeDefined();
    expect(text()).not.toContain('Report Game');
  });

  it('offers no reschedule link to a player the server does not allow (not on either team, or the match decided)', () => {
    isEventOrganizer.set(false);
    isPlayer.set(true);
    load(page({ canRequestReschedule: false }));
    expect(button('Reschedule')).toBeUndefined();
  });

  it('shows the report tools to organizers while the match is undecided', () => {
    load(page(), [possible]);
    expect(text()).toContain('Report Game');
    expect(button('Home Team Forfeit')).toBeDefined();
    expect(button('Away Team Forfeit')).toBeDefined();
  });

  it('hides the report form once the match is complete', () => {
    load(page({ homeTeamResult: 'Won', awayTeamResult: 'Loss' }));
    expect(text()).toContain('This match has been completed.');
    expect(button('Home Team Forfeit')).toBeUndefined();
  });

  it('renders each possible match with both teams\' player stats', () => {
    load(page(), [possible]);
    expect(text()).toContain('abc-123: 50 - 49');
    expect((fixture.nativeElement as HTMLElement).querySelector('.lobby-possible__title a')!.getAttribute('href')).toBe('/match/abc-123');
    const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row'))
      .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));
    expect(rows).toEqual([['Grif', '11', '12', '13'], ['Tucker', '7', '8', '9']]);
  });

  it('reports a possible match and reloads the page afterwards', () => {
    load(page(), [possible]);
    button('SUBMIT')!.click();
    fixture.detectChanges();

    expect(component.isSubmittingMatch()).toBeTrue();
    expect(button('Home Team Forfeit')!.disabled).toBeTrue();

    httpMock.expectOne({ method: 'POST', url: '/api/SeasonMatch/ReportMatch/7/abc-123' }).flush('ok');
    expect(component.isSubmittingMatch()).toBeFalse();
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page({ homeTeamResult: 'Won', awayTeamResult: 'Loss' }));
    fixture.detectChanges();
    expect(text()).toContain('This match has been completed.');
  });

  it('shows the possible matches to everyone, the SUBMIT buttons to organizers only', () => {
    isEventOrganizer.set(false);
    load(page(), [possible]);
    expect(text()).toContain('Possible Matches');
    expect(text()).toContain('abc-123: 50 - 49');
    expect(text()).not.toContain('Report Game');
    expect(button('SUBMIT')).toBeUndefined();
  });

  it('says it is looking for games until the search answers, then lists them', () => {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    fixture.detectChanges();
    httpMock.match(r => r.url.startsWith('/api/Team/Team/')).forEach(r => r.flush(null));
    fixture.detectChanges();
    const section = () => (fixture.nativeElement as HTMLElement).querySelector('.lobby-possibles')!;
    expect(section().textContent).toContain('Looking for games…');
    expect(section().getAttribute('aria-busy')).toBe('true');

    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([possible]);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    fixture.detectChanges();
    expect(section().textContent).not.toContain('Looking for games…');
    expect(section().textContent).toContain('abc-123: 50 - 49');
    expect(section().getAttribute('aria-busy')).toBe('false');
  });

  it('says when no games were found', () => {
    load(page(), []);
    expect(text()).toContain('No games found for these teams yet.');
  });

  it('says the search failed when it does, with no list', () => {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    fixture.detectChanges();
    httpMock.match(r => r.url.startsWith('/api/Team/Team/')).forEach(r => r.flush(null));
    fixture.detectChanges();
    expect(component.possibleMatchesState()).toBe('error');
    expect(text()).toContain('Could not look for games right now.');
    expect(text()).not.toContain('No games found');
  });

  it('says when the players\' games were last pulled, or that the last pull failed', () => {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush({ ...idlePull, lastRun: { firedAt: '2026-11-01T18:00:00Z', durationMs: 900, result: 'Failed', error: 'Halo is down' } });
    fixture.detectChanges();
    httpMock.match(r => r.url.startsWith('/api/Team/Team/')).forEach(r => r.flush(null));
    fixture.detectChanges();
    expect(text()).toContain('The last pull failed');
    expect(button('Pull recent games')!.disabled).toBeFalse();
  });

  it('pulls the players\' recent games as a job, watches it, then reads the games it found', () => {
    jasmine.clock().install();
    try {
      load(page());
      press('Pull recent games');
      httpMock.expectOne({ method: 'POST', url: '/api/SeasonMatch/PullMatches/7' }).flush({ ...idlePull, state: 'Queued' });
      fixture.detectChanges();
      expect(text()).toContain('waiting for the worker');
      expect(button('Pull recent games')!.disabled).toBeTrue();

      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush({ ...idlePull, state: 'Running', startedAt: '2026-11-01T18:00:00Z' });
      fixture.detectChanges();
      expect(text()).toContain('Pulling the players\' recent games from Halo Infinite…');

      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush({ ...idlePull, lastRun: { firedAt: '2026-11-01T18:01:00Z', durationMs: 60000, result: 'Succeeded', error: null } });
      httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([possible]);
      fixture.detectChanges();
      expect(text()).toContain('Recent games last pulled');
      expect(text()).toContain('abc-123: 50 - 49');
      expect(button('Pull recent games')!.disabled).toBeFalse();

      jasmine.clock().tick(PULL_POLL_MS * 2);
      httpMock.expectNone('/api/SeasonMatch/MatchPullStatus/7');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it("pulls back to the season's start when asked, for an old match whose games are past the players' recent ones", () => {
    jasmine.clock().install();
    try {
      load(page());
      press("Pull back to the season's start");
      httpMock.expectOne({ method: 'POST', url: '/api/SeasonMatch/PullMatches/7?backToSeasonStart=true' }).flush({ ...idlePull, state: 'Queued' });
      fixture.detectChanges();
      expect(button('Pull recent games')!.disabled).toBeTrue();
      expect(button("Pull back to the season's start")!.disabled).toBeTrue();

      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush({ ...idlePull, lastRun: { firedAt: '2026-11-01T18:01:00Z', durationMs: 60000, result: 'Succeeded', error: null } });
      httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([possible]);
      fixture.detectChanges();
      expect(button("Pull back to the season's start")!.disabled).toBeFalse();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('says why a pull could not be started', () => {
    load(page());
    press('Pull recent games');
    httpMock.expectOne({ method: 'POST', url: '/api/SeasonMatch/PullMatches/7' }).flush('Forbidden', { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(component.pullError()).toBeTruthy();
    expect(component.pulling()).toBeFalse();
  });

  it('offers the pull to organizers only', () => {
    isEventOrganizer.set(false);
    load(page());
    expect(button('Pull recent games')).toBeUndefined();
  });

  it('shows no pull status when it cannot be read, and says when it lost track of a pull', () => {
    jasmine.clock().install();
    try {
      httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
      httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
      httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush('no', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();
      httpMock.match(r => r.url.startsWith('/api/Team/Team/')).forEach(r => r.flush(null));
      expect(component.pull()).toBeNull();

      component.pullMatches();
      httpMock.expectOne('/api/SeasonMatch/PullMatches/7').flush({ ...idlePull, state: 'Running' });
      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush('down', { status: 502, statusText: 'Bad Gateway' });
      expect(component.pullError()).toContain("Couldn't tell whether the pull finished");
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('treats an empty answer as no games', () => {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush(null);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    expect(component.possibleMatches()).toEqual([]);
    expect(component.possibleMatchesState()).toBe('loaded');
    httpMock.match(r => r.url.startsWith('/api/Team/Team/')).forEach(r => r.flush(null));
  });

  it('clears the submitting state and reloads even when reporting fails', () => {
    load(page());
    component.onSubmit('bad');
    httpMock.expectOne({ method: 'POST', url: '/api/SeasonMatch/ReportMatch/7/bad' }).flush('no', { status: 400, statusText: 'Bad Request' });

    expect(component.isSubmittingMatch()).toBeFalse();
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
  });

  function press(label: string): void {
    button(label)!.click();
    fixture.detectChanges();
  }

  it('asks for confirmation before recording a home forfeit', () => {
    load(page());
    press('Home Team Forfeit');

    httpMock.expectNone('/api/SeasonMatch/HomeForfeit/7');
    expect(text()).toContain('The home team forfeits and the away team wins.');
    expect(button('Home Team Forfeit')).toBeUndefined();

    press('Confirm');
    expect(component.isSubmittingMatch()).toBeTrue();

    const req = httpMock.expectOne('/api/SeasonMatch/HomeForfeit/7');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    expect(component.isSubmittingMatch()).toBeFalse();
  });

  it('records an away forfeit, reloading even on failure', () => {
    load(page());
    press('Away Team Forfeit');
    expect(text()).toContain('The away team forfeits and the home team wins.');
    press('Confirm');

    const req = httpMock.expectOne('/api/SeasonMatch/AwayForfeit/7');
    expect(req.request.method).toBe('POST');
    req.flush('no', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    expect(component.isSubmittingMatch()).toBeFalse();
    fixture.detectChanges();
    expect(component.actionError()).toBe('no');
    expect(text()).toContain('no');
  });

  it('records a forfeit by both teams once confirmed', () => {
    load(page({ isPlayoff: true, bracketInfo: bracket() }));
    press('Both Teams Forfeit');
    expect(text()).toContain('Both teams forfeit and take a loss.');
    press('Confirm');

    const req = httpMock.expectOne('/api/SeasonMatch/DoubleForfeit/7');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
  });

  it('records nothing when a forfeit is cancelled', () => {
    load(page());
    press('Both Teams Forfeit');
    press('Cancel');

    httpMock.expectNone('/api/SeasonMatch/DoubleForfeit/7');
    expect(component.pendingAction()).toBeNull();
    expect(button('Both Teams Forfeit')).toBeDefined();
  });

  it('confirming does nothing when no action is pending', () => {
    load(page());
    component.confirmAction();
    expect(component.isSubmittingMatch()).toBeFalse();
  });

  it('offers no forfeit for the grand final or its sudden death, only reporting', () => {
    load(page({ isPlayoff: true, bracketInfo: bracket({ isGrandFinal: true }) }));

    expect(text()).not.toContain('Forfeit');
    expect(text()).toContain('Enter Match ID');
  });

  it('undoes a result once confirmed', () => {
    load(page({ homeTeamResult: 'Forfeit', awayTeamResult: 'Won' }));
    press('Undo Result');

    httpMock.expectNone('/api/SeasonMatch/UndoResult/7');
    expect(text()).toContain('This match goes back to undecided');
    press('Confirm');

    const req = httpMock.expectOne('/api/SeasonMatch/UndoResult/7');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    fixture.detectChanges();
    expect(button('Undo Result')).toBeUndefined();
    expect(button('Home Team Forfeit')).toBeDefined();
  });

  it('shows why the server would not undo a result', () => {
    load(page({ homeTeamResult: 'Won', awayTeamResult: 'Loss' }));
    press('Undo Result');
    press('Confirm');

    httpMock.expectOne('/api/SeasonMatch/UndoResult/7').flush('Undo that match first.', { status: 400, statusText: 'Bad Request' });
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page({ homeTeamResult: 'Won', awayTeamResult: 'Loss' }));
    fixture.detectChanges();
    expect(text()).toContain('Undo that match first.');
  });

  it('offers undo for reported games of an undecided series', () => {
    load(page({ reportedGames: [{ matchNumber: 1, matchID: 'g-1' }] }));
    expect(button('Undo Result')).toBeDefined();
    expect(button('Home Team Forfeit')).toBeDefined();
  });

  it('offers no undo without a result, or for a match with a BYE', () => {
    load(page());
    expect(button('Undo Result')).toBeUndefined();

    component.seasonMatch.set(page({ isPlayoff: true, homeTeamResult: 'Won', awayTeamName: null, awayTeamID: null, awayTeamResult: 'Bye' }));
    fixture.detectChanges();
    expect(button('Undo Result')).toBeUndefined();
    expect(text()).toContain('This match has been completed.');
  });

  it('only accepts a GUID as a match id', async () => {
    load(page());
    await fixture.whenStable();
    const input = (fixture.nativeElement as HTMLElement).querySelector('input[name="MatchID"]') as HTMLInputElement;
    const submit = button('Submit')!;

    input.value = 'nope';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(submit.disabled).toBeTrue();
    expect(text()).toContain('Must be a valid GUID');

    input.value = '0f8fad5b-d9cb-469f-a165-70867728950e';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(submit.disabled).toBeFalse();
  });
});

describe('SeasonMatchComponent lobby', () => {
  let fixture: ComponentFixture<SeasonMatchComponent>;
  let httpMock: HttpTestingController;

  const page = (overrides: Partial<SeasonMatchPageDto> = {}): SeasonMatchPageDto => ({
    seasonID: 3, seasonName: 'Season 3', isPlayoff: false,
    homeTeamName: 'Home', homeTeamID: 10, homeTeamScore: null, homeTeamResult: 'TBD',
    awayTeamName: 'Away', awayTeamID: 20, awayTeamScore: null, awayTeamResult: 'TBD',
    scheduledTime: null, bestOf: 3, reportedGames: [], bracketInfo: null, activeRescheduleRequestId: null, canRequestReschedule: false,
    ...overrides
  });
  const team = (name: string, captain: string, player: string) => ({
    teamName: name, wins: 0, losses: 0, tbd: 0,
    players: [
      { teamPlayerID: 1, userID: 1, gamertag: captain, draftCaptainOrder: 1, draftRound: null },
      { teamPlayerID: 2, userID: 2, gamertag: player, draftCaptainOrder: null, draftRound: 1 },
    ],
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { isEventOrganizer: signal(false), isPlayer: signal(false) } }
      ]
    }).overrideComponent(SeasonMatchComponent, withoutVodList).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonMatchComponent);
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function load(dto: SeasonMatchPageDto): void {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(dto);
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    fixture.detectChanges();
  }
  const el = () => fixture.nativeElement as HTMLElement;
  const roster = (side: 'home' | 'away') => Array.from(el().querySelectorAll(`.lobby-team--${side} .lobby-player`))
    .map(p => Array.from(p.children).map(c => c.textContent!.trim()).join(' '));

  it("lists each side's players from its team, marking the captain", () => {
    load(page());
    httpMock.expectOne('/api/Team/Team/10').flush(team('Home', 'Grif', 'Simmons'));
    httpMock.expectOne('/api/Team/Team/20').flush(team('Away', 'Tucker', 'Caboose'));
    fixture.detectChanges();

    expect(roster('home')).toEqual(['G Grif Captain', 'S Simmons']);
    expect(roster('away')).toEqual(['Captain Tucker T', 'Caboose C']);
  });

  it('shows players without a gamertag (no Xbox account linked) without breaking the page', () => {
    load(page());
    httpMock.expectOne('/api/Team/Team/10').flush({ ...team('Home', 'Grif', 'x'), players: [{ teamPlayerID: 3, userID: 3, gamertag: null, draftCaptainOrder: 1, draftRound: null }] });
    httpMock.expectOne('/api/Team/Team/20').flush({ ...team('Away', 'Tucker', 'x'), players: [{ teamPlayerID: 4, userID: 4, gamertag: null, draftCaptainOrder: null, draftRound: 1 }] });
    fixture.detectChanges();

    expect(roster('home')).toEqual(['? Unknown Spartan Captain']);
    expect(roster('away')).toEqual(['Unknown Spartan ?']);
    expect(el().querySelector('.lobby-possibles')!.textContent).toContain('No games found for these teams yet.');
  });

  it('keeps the page hidden until the match and both rosters are in', () => {
    const pageEl = () => el().querySelector('.hud-page')!;
    expect(pageEl().classList).toContain('hud-waiting');
    load(page());
    expect(fixture.componentInstance.ready()).toBeFalse();
    httpMock.expectOne('/api/Team/Team/10').flush(team('Home', 'Grif', 'Simmons'));
    fixture.detectChanges();
    expect(pageEl().classList).toContain('hud-waiting');
    httpMock.expectOne('/api/Team/Team/20').flush(team('Away', 'Tucker', 'Caboose'));
    fixture.detectChanges();
    expect(pageEl().classList).not.toContain('hud-waiting');
  });

  it('shows the page for a match that does not exist, or that could not be loaded', () => {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush('x', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
    httpMock.expectOne('/api/SeasonMatch/MatchPullStatus/7').flush(idlePull);
    fixture.detectChanges();
    expect(fixture.componentInstance.ready()).toBeTrue();
  });

  it('does not reload the rosters when the match page is reloaded for the same teams', () => {
    load(page());
    httpMock.expectOne('/api/Team/Team/10').flush(team('Home', 'Grif', 'Simmons'));
    httpMock.expectOne('/api/Team/Team/20').flush(null);
    fixture.componentInstance.finishedReportingMatch();
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page({ homeTeamScore: 1, awayTeamScore: 0 }));
    fixture.detectChanges();

    httpMock.expectNone(r => r.url.startsWith('/api/Team/Team/'));
    expect(roster('home').length).toBe(2);
    expect(roster('away')).toEqual([]);
  });

  it('leaves a roster empty when it cannot be loaded, and has none for a BYE', () => {
    load(page({ isPlayoff: true, awayTeamName: null, awayTeamID: null, awayTeamResult: 'Bye', homeTeamResult: 'Won' }));
    httpMock.expectOne('/api/Team/Team/10').flush('x', { status: 500, statusText: 'Server Error' });
    httpMock.expectNone('/api/Team/Team/null');
    fixture.detectChanges();

    expect(roster('home')).toEqual([]);
    expect(roster('away')).toEqual([]);
  });

  it('is a lobby until a result is in, then a match report with the score', () => {
    load(page());
    httpMock.match(() => true).forEach(r => r.flush(null));
    expect(el().querySelector('h1')!.textContent).toBe('Pre-game lobby');
    expect(el().querySelector('.lobby-score__value')!.textContent!.replace(/\s+/g, '')).toBe('-:-');

    fixture.componentInstance.seasonMatch.set(page({ homeTeamScore: 2, awayTeamScore: 1, homeTeamResult: 'Won', awayTeamResult: 'Loss' }));
    fixture.detectChanges();
    expect(el().querySelector('h1')!.textContent).toBe('Match report');
    expect(el().querySelector('.lobby-score__value')!.textContent!.replace(/\s+/g, '')).toBe('2:1');
    expect(el().querySelector('.lobby-score__results')!.textContent).toContain('Won');
    expect(el().querySelector('.lobby-head__season')!.textContent).toContain('Season 3 · Regular season');
  });

  it('has no series totals tab for a single game', () => {
    load(page());
    httpMock.match(() => true).forEach(r => r.flush(null));
    const comp = fixture.componentInstance;

    comp.seasonMatch.set(page({
      reportedGames: [
        {
          matchNumber: 1,
          matchID: '123',
          homePlayers: [{ userID: 1, gamertag: 'Solo', kills: 5, deaths: 2, assists: 0, bombScores: 1, score: null }],
          awayPlayers: [],
        }
      ]
    }));
    fixture.detectChanges();
    expect(comp.availableTabs().length).toBe(1);
    expect(comp.availableTabs()[0].label).toBe('Game 1');
    expect(comp.activeScoreboard()?.title).toBe('Game 1');
  });
});

describe('SeasonMatchComponent on the server', () => {
  it('renders the page without asking for the possible matches (slow), which the browser loads', async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'server' },
        { provide: AccountService, useValue: { isEventOrganizer: signal(true), isPlayer: signal(false) } },
      ],
    }).overrideComponent(SeasonMatchComponent, withoutVodList).compileComponents();
    const httpMock = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SeasonMatchComponent);
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();

    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7');
    httpMock.expectNone('/api/SeasonMatch/GetPossibleMatches/7');
    // The page drawn on the server says the search is under way; the browser does it
    expect(fixture.componentInstance.possibleMatchesState()).toBe('loading');
    httpMock.verify();
  });
});
