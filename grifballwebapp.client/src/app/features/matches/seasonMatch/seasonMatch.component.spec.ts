import { DateTime } from 'luxon';
import { DatePipe } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describeSeasonMatch, PULL_POLL_MS, SeasonMatchComponent } from './seasonMatch.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { ActivatedRoute, provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { Component, input, output, PLATFORM_ID, signal } from '@angular/core';
import { ManualGamesComponent } from './manualGames/manualGames.component';
import { AccountService } from '@core/auth/account.service';
import { BracketInfoDto, ReportedGameDto, SeasonMatchPageDto } from './seasonMatchPageDto';
import type { PossibleMatchDto, PossiblePlayerDto } from '@api/models';
import { VodListComponent } from '@shared/vodList/vodList.component';
import { StubVodListComponent } from '@shared/vodList/vodList.stub';
import type { OnDemandJobStatusDto } from '@api/models';

/** Manual entry is tested on its own (manualGames.component.spec.ts): here a stub, which asks for nothing. */
@Component({ selector: 'app-manual-games', template: '' })
class StubManualGamesComponent {
  readonly seasonMatchID = input<number>();
  readonly match = input<SeasonMatchPageDto | null>();
  readonly homeRoster = input<unknown[]>();
  readonly awayRoster = input<unknown[]>();
  readonly canEdit = input<boolean>();
  readonly blocked = input<boolean>();
  readonly changed = output<void>();
  readonly started = output<void>();
  readonly idle = signal(true);
  readonly saving = signal(false);
  canChange(game: ReportedGameDto | null | undefined): boolean {
    return !!game?.isManual && !!this.canEdit();
  }
  canRemove(game: ReportedGameDto | null | undefined): boolean {
    return !!game && !!this.canEdit();
  }
  readonly startEdit = jasmine.createSpy('startEdit');
  readonly requestDelete = jasmine.createSpy('requestDelete');
  readonly cancel = jasmine.createSpy('cancel');
}

/** The VOD list and manual entry are tested on their own: here stubs, so the page makes no request for them. */
const withoutVodList = { remove: { imports: [VodListComponent, ManualGamesComponent] }, add: { imports: [StubVodListComponent, StubManualGamesComponent] } };
/** Decided by its games, by a forfeit, and by a BYE on either side, as the server sends each (decidedBy, canUndo). */
const wonByGames: Partial<SeasonMatchPageDto> = { homeTeamResult: 'Won', awayTeamResult: 'Loss', decidedBy: 'Games', canUndo: true };
const forfeited: Partial<SeasonMatchPageDto> = { homeTeamResult: 'Forfeit', awayTeamResult: 'Won', decidedBy: 'Forfeit', canUndo: true };
const awayBye: Partial<SeasonMatchPageDto> = { homeTeamResult: 'Won', awayTeamName: null, awayTeamID: null, awayTeamResult: 'Bye', decidedBy: 'Bye', canUndo: false };
const homeBye: Partial<SeasonMatchPageDto> = { homeTeamName: null, homeTeamID: null, homeTeamResult: 'Bye', awayTeamResult: 'Won', decidedBy: 'Bye', canUndo: false };

/** No pull queued or running, and none in the history kept. */
const idlePull: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };

// A match page with every field set as most tests want it, and their own changes on top
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
  ...overrides
});

// The page's three requests on open answered with the match, then the change detection that renders it
function loadPage(httpMock: HttpTestingController, fixture: ComponentFixture<SeasonMatchComponent>, dto: SeasonMatchPageDto): void {
  httpMock.expectOne('/api/season-matches/7').flush(dto);
  httpMock.expectOne('/api/season-matches/7/possible-matches').flush([]);
  httpMock.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
  fixture.detectChanges();
}

describe('SeasonMatchComponent', () => {
  let component: SeasonMatchComponent;
  let fixture: ComponentFixture<SeasonMatchComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
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
          useValue: { isEventOrganizer: signal(false), isPlayer: signal(false), isSysAdmin: signal(false) }
        }
      ]
    }).overrideComponent(SeasonMatchComponent, withoutVodList).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonMatchComponent);
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();
  });

  const load = (dto: SeasonMatchPageDto): void => loadPage(httpMock, fixture, dto);

  it('drops the previous match, and its title, while another loads', () => {
    load(page({ homeTeamName: 'Old Home' }));
    expect(document.title).toContain('Old Home');

    fixture.componentRef.setInput('seasonMatchID', 8);
    fixture.detectChanges();
    expect(fixture.componentInstance.seasonMatch()).toBeNull();
    expect(document.title).not.toContain('Old Home');
  });

  it('cancels the previous match\'s request when the match changes before it answers', () => {
    const first = httpMock.expectOne('/api/season-matches/7');
    fixture.componentRef.setInput('seasonMatchID', 8);
    fixture.detectChanges();
    expect(first.cancelled).toBeTrue();
  });

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
    httpMock.expectOne('/api/season-matches/7').flush(null, { status: 204, statusText: 'No Content' });
    httpMock.expectOne('/api/season-matches/7/possible-matches').flush([]);
    httpMock.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
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
    load(page({ isPlayoff: true, ...awayBye }));

    expect(teamTiles()).toEqual([{ text: 'Home', linked: true }, { text: 'BYE', linked: false }]);
  });

  it('shows a home BYE without a team link', () => {
    load(page({ isPlayoff: true, ...homeBye }));

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
  let isSysAdmin: ReturnType<typeof signal<boolean>>;

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
    isSysAdmin = signal(false);
    spyOn(console, 'log');
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { isEventOrganizer, isPlayer, isSysAdmin } }
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
    httpMock.expectOne('/api/season-matches/7').flush(dto);
    httpMock.expectOne('/api/season-matches/7/possible-matches').flush(matches);
    httpMock.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
    fixture.detectChanges();
    // The rosters are not under test here.
    httpMock.match(r => /^\/api\/teams\/\w+$/.test(r.url)).forEach(r => r.flush(null));
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
    .find(b => b.textContent!.trim() === label) as HTMLButtonElement | undefined;

  it('lists reported games and scoreboards', () => {
    load(page({
      reportedGames: [
        {
          seriesGameID: 1,
          matchNumber: 1,
          matchID: 'g-1',
          homePlayers: [{ userID: 1, gamertag: 'Chief', kills: 20, deaths: 10, assists: 2, bombScores: 3, score: null }],
          awayPlayers: [{ userID: 2, gamertag: 'Arbiter', kills: 10, deaths: 20, assists: 1, bombScores: 1, score: null }]
        },
        { seriesGameID: 502, matchNumber: 2, matchID: 'g-2' }
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
          seriesGameID: 1,
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
      reportedGames: [{ seriesGameID: 501, matchNumber: 1, matchID: 'g-1' }, { seriesGameID: 502, matchNumber: 2, matchID: 'g-2' }],
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
    load(page({ reportedGames: [{ seriesGameID: 501, matchNumber: 1, matchID: 'g-1', homeScore: 5, awayScore: 3 }] }));
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
      reportedGames: [{ seriesGameID: 501, matchNumber: 1, matchID: 'g-1', homeScore: 5, awayScore: 2, homePlayers: [], awayPlayers: [] }],
    }));
    const header = (side: string) => Array.from((fixture.nativeElement as HTMLElement).querySelector(`.scoreboard-team--${side} .scoreboard-team-header`)!.children)
      .map(c => c.textContent!.trim()).join(' ');
    expect(header('home')).toBe('Home Red Rampage 5');
    expect(header('away')).toBe('Away Blue Bombers 2');
  });

  it('shows a row for every player, even two with the same gamertag', () => {
    const unknown = { userID: null, gamertag: 'Unknown Spartan', kills: 1, deaths: 1, assists: 0, bombScores: 0, score: null };
    load(page({
      reportedGames: [{ seriesGameID: 501, matchNumber: 1, matchID: 'g-1', homePlayers: [{ ...unknown, kills: 4 }, { ...unknown, kills: 2 }], awayPlayers: [] }],
    }));
    const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.scoreboard-team--home tbody tr'));
    expect(rows.map(r => r.children[1].textContent!.trim())).toEqual(['4', '2']);
  });

  const manualGame = (overrides: Partial<ReportedGameDto> = {}): ReportedGameDto => ({
    seriesGameID: 51, matchNumber: 1, matchID: null, recordedGameID: 41, homeScore: 5, awayScore: 2, homePlayers: [], awayPlayers: [],
    isManual: true, gameVersionID: 1, gameVersionName: 'Halo 3', map: 'Foundry', gametype: null, playedAt: '2026-01-10T20:30:00Z',
    ...overrides,
  });
  const manualEntry = () => fixture.debugElement.query(By.directive(StubManualGamesComponent)).componentInstance as StubManualGamesComponent;

  it('shows a game entered by hand in the box score, once, with its Edit and Unreport handed to manual entry', () => {
    const game = manualGame();
    load(page({ reportedGames: [game], canUndo: true }));

    expect(text()).not.toContain('There are no reported games');
    expect(text()).toContain('Entered by hand');
    expect(text()).toContain('Halo 3 · Foundry');
    expect(text()).not.toContain('Recorded Game #41');
    expect(component.canUndo()).toBeTrue();
    const entry = manualEntry();
    expect(entry.match()?.reportedGames).toEqual([game]);
    expect(entry.canEdit()).toBeTrue();

    button('Edit')!.click();
    expect(entry.startEdit).toHaveBeenCalledWith(game);
    expect(button('Unreport')!.getAttribute('aria-label')).toBe('Unreport game 1');
    button('Unreport')!.click();
    expect(entry.requestDelete).toHaveBeenCalledWith(game);

    // Not while an entry or a delete is under way
    entry.idle.set(false);
    fixture.detectChanges();
    expect(button('Edit')).toBeUndefined();

    entry.changed.emit();
    httpMock.expectOne('/api/season-matches/7').flush(page());
  });

  it('shows an imported recorded game by its number, to unreport but not edit', () => {
    load(page({ reportedGames: [manualGame({ isManual: false, map: null, playedAt: null })] }));

    expect(text()).toContain('Recorded Game #41');
    expect(text()).toContain('Halo 3');
    expect(text()).not.toContain('Entered by hand');
    expect(button('Edit')).toBeUndefined();
    expect(button('Unreport')).toBeDefined();
  });

  it('offers the pull buttons to any player, and to a sysadmin, as to an organizer', () => {
    isEventOrganizer.set(false);
    isPlayer.set(true);
    load(page());
    expect(button('Pull recent games')).toBeDefined();
    expect(button("Pull back to the season's start")).toBeDefined();

    isPlayer.set(false);
    isSysAdmin.set(true);
    fixture.detectChanges();
    expect(button('Pull recent games')).toBeDefined();
  });

  it('offers no pull buttons to a visitor', () => {
    isEventOrganizer.set(false);
    load(page());
    expect(button('Pull recent games')).toBeUndefined();
    expect(button("Pull back to the season's start")).toBeUndefined();
  });

  it('offers no Edit or Unreport to a visitor', () => {
    isEventOrganizer.set(false);
    load(page({ reportedGames: [manualGame()] }));

    expect(text()).toContain('Entered by hand');
    expect(button('Edit')).toBeUndefined();
    expect(button('Unreport')).toBeUndefined();
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
    load(page({ ...wonByGames }));
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

    expect(component.submitting()).toBeTrue();
    expect(button('Home Team Forfeit')!.disabled).toBeTrue();

    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc-123/report' }).flush('ok');
    // Reported, but the page still shows the match as it was: SUBMIT waits for the reload
    fixture.detectChanges();
    expect(component.submitting()).toBeTrue();
    expect(button('SUBMIT')!.disabled).toBeTrue();
    httpMock.expectOne('/api/season-matches/7').flush(page({ ...wonByGames }));
    fixture.detectChanges();
    expect(component.submitting()).toBeFalse();
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
    httpMock.expectOne('/api/season-matches/7').flush(page());
    fixture.detectChanges();
    httpMock.match(r => /^\/api\/teams\/\w+$/.test(r.url)).forEach(r => r.flush(null));
    fixture.detectChanges();
    const section = () => (fixture.nativeElement as HTMLElement).querySelector('.lobby-possibles')!;
    expect(section().textContent).toContain('Looking for games…');
    expect(section().getAttribute('aria-busy')).toBe('true');

    httpMock.expectOne('/api/season-matches/7/possible-matches').flush([possible]);
    httpMock.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
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
    httpMock.expectOne('/api/season-matches/7').flush(page());
    httpMock.expectOne('/api/season-matches/7/possible-matches').flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
    fixture.detectChanges();
    httpMock.match(r => /^\/api\/teams\/\w+$/.test(r.url)).forEach(r => r.flush(null));
    fixture.detectChanges();
    expect(component.possibleMatchesState()).toBe('error');
    expect(text()).toContain('Could not look for games right now.');
    expect(text()).not.toContain('No games found');
  });

  it('says when the players\' games were last pulled, or that the last pull failed', () => {
    httpMock.expectOne('/api/season-matches/7').flush(page());
    httpMock.expectOne('/api/season-matches/7/possible-matches').flush([]);
    httpMock.expectOne('/api/season-matches/7/match-pull').flush({ ...idlePull, lastRun: { firedAt: '2026-11-01T18:00:00Z', durationMs: 900, result: 'Failed', error: 'Halo is down' } });
    fixture.detectChanges();
    httpMock.match(r => /^\/api\/teams\/\w+$/.test(r.url)).forEach(r => r.flush(null));
    fixture.detectChanges();
    expect(text()).toContain('The last pull failed');
    expect(button('Pull recent games')!.disabled).toBeFalse();
  });

  it('pulls the players\' recent games as a job, watches it, then reads the games it found', () => {
    jasmine.clock().install();
    try {
      load(page());
      press('Pull recent games');
      httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/match-pull' }).flush({ ...idlePull, state: 'Queued' });
      fixture.detectChanges();
      expect(text()).toContain('waiting for the worker');
      expect(button('Pull recent games')!.disabled).toBeTrue();

      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/season-matches/7/match-pull').flush({ ...idlePull, state: 'Running', startedAt: '2026-11-01T18:00:00Z' });
      fixture.detectChanges();
      expect(text()).toContain('Pulling the players\' recent games from Halo Infinite…');

      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/season-matches/7/match-pull').flush({ ...idlePull, lastRun: { firedAt: '2026-11-01T18:01:00Z', durationMs: 60000, result: 'Succeeded', error: null } });
      httpMock.expectOne('/api/season-matches/7/possible-matches').flush([possible]);
      fixture.detectChanges();
      expect(text()).toContain('Recent games last pulled');
      expect(text()).toContain('abc-123: 50 - 49');
      expect(button('Pull recent games')!.disabled).toBeFalse();

      jasmine.clock().tick(PULL_POLL_MS * 2);
      httpMock.expectNone('/api/season-matches/7/match-pull');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it("pulls back to the season's start when asked, for an old match whose games are past the players' recent ones", () => {
    jasmine.clock().install();
    try {
      load(page());
      press("Pull back to the season's start");
      httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/match-pull?backToSeasonStart=true' }).flush({ ...idlePull, state: 'Queued' });
      fixture.detectChanges();
      expect(button('Pull recent games')!.disabled).toBeTrue();
      expect(button("Pull back to the season's start")!.disabled).toBeTrue();

      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/season-matches/7/match-pull').flush({ ...idlePull, lastRun: { firedAt: '2026-11-01T18:01:00Z', durationMs: 60000, result: 'Succeeded', error: null } });
      httpMock.expectOne('/api/season-matches/7/possible-matches').flush([possible]);
      fixture.detectChanges();
      expect(button("Pull back to the season's start")!.disabled).toBeFalse();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('says why a pull could not be started', () => {
    load(page());
    press('Pull recent games');
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/match-pull' }).flush('Forbidden', { status: 403, statusText: 'Forbidden' });
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
      httpMock.expectOne('/api/season-matches/7').flush(page());
      httpMock.expectOne('/api/season-matches/7/possible-matches').flush([]);
      httpMock.expectOne('/api/season-matches/7/match-pull').flush('no', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();
      httpMock.match(r => /^\/api\/teams\/\w+$/.test(r.url)).forEach(r => r.flush(null));
      expect(component.pull()).toBeNull();

      component.pullMatches();
      httpMock.expectOne('/api/season-matches/7/match-pull').flush({ ...idlePull, state: 'Running' });
      jasmine.clock().tick(PULL_POLL_MS);
      httpMock.expectOne('/api/season-matches/7/match-pull').flush('down', { status: 502, statusText: 'Bad Gateway' });
      expect(component.pullError()).toContain("Couldn't tell whether the pull finished");
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('treats an empty answer as no games', () => {
    httpMock.expectOne('/api/season-matches/7').flush(page());
    httpMock.expectOne('/api/season-matches/7/possible-matches').flush(null);
    httpMock.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
    expect(component.possibleMatches()).toEqual([]);
    expect(component.possibleMatchesState()).toBe('loaded');
    httpMock.match(r => /^\/api\/teams\/\w+$/.test(r.url)).forEach(r => r.flush(null));
  });

  it('asks whether a game reported to a series with games is new or replaces one, never picking for the commissioner', () => {
    const typed = manualGame();
    load(page({ reportedGames: [typed] }), [possible]);
    button('SUBMIT')!.click();
    fixture.detectChanges();
    httpMock.expectNone(r => /\/games\/[^/]+\/report$/.test(r.url));
    expect(text()).toContain('Is this a new game of the series, or does it replace a game already reported?');

    button('Replaces game 1 (entered by hand)')!.click();
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc-123/report?replaces=51' }).flush('ok');
    httpMock.expectOne('/api/season-matches/7').flush(page({ reportedGames: [typed] }));
    fixture.detectChanges();

    button('SUBMIT')!.click();
    fixture.detectChanges();
    button('A new game (game 2)')!.click();
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc-123/report' }).flush('ok');
    httpMock.expectOne('/api/season-matches/7').flush(page());

    component.onSubmit('abc-123');
    expect(component.placingReport()).toBeNull();
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc-123/report' }).flush('ok');
    httpMock.expectOne('/api/season-matches/7').flush(page());
  });

  it('clears the submitting state once reloaded, and says why, when reporting fails', () => {
    load(page());
    component.onSubmit('bad');
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/bad/report' }).flush({ status: 400, title: 'Request refused', detail: 'Not a game of these teams' }, { status: 400, statusText: 'Bad Request' });

    expect(component.actionError()).toBe('Not a game of these teams');
    expect(component.submitting()).toBeTrue();
    httpMock.expectOne('/api/season-matches/7').flush(page());
    expect(component.submitting()).toBeFalse();
  });

  it('stays submitting until the reload is in, so no tool is offered against the match as it was', () => {
    load(page());
    press('Home Team Forfeit');
    press('Confirm');
    httpMock.expectOne('/api/season-matches/7/forfeit/home').flush(null);
    fixture.detectChanges();

    // Done on the server, but the page still shows the match undecided: everything waits
    expect(component.submitting()).toBeTrue();
    expect(button('Away Team Forfeit')!.disabled).toBeTrue();
    component.requestAction('AwayForfeit');
    expect(component.confirming()).toBeNull();

    httpMock.expectOne('/api/season-matches/7').flush(page({ ...forfeited }));
    fixture.detectChanges();
    expect(component.submitting()).toBeFalse();
    expect(text()).toContain('This match has been completed.');
    expect(button('Away Team Forfeit')).toBeUndefined();
  });

  it('is idle again even when the reload fails', () => {
    load(page());
    component.requestAction('DoubleForfeit');
    component.confirmAction();
    httpMock.expectOne('/api/season-matches/7/forfeit/double').flush(null);
    expect(component.submitting()).toBeTrue();
    httpMock.expectOne('/api/season-matches/7').flush('down', { status: 500, statusText: 'Server Error' });
    expect(component.submitting()).toBeFalse();
  });

  function press(label: string): void {
    button(label)!.click();
    fixture.detectChanges();
  }

  it('asks for confirmation before recording a home forfeit', () => {
    load(page());
    press('Home Team Forfeit');

    httpMock.expectNone('/api/season-matches/7/forfeit/home');
    expect(text()).toContain('The home team forfeits and the away team wins.');
    expect(button('Home Team Forfeit')).toBeUndefined();

    press('Confirm');
    expect(component.submitting()).toBeTrue();

    const req = httpMock.expectOne('/api/season-matches/7/forfeit/home');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    httpMock.expectOne('/api/season-matches/7').flush(page());
    expect(component.submitting()).toBeFalse();
  });

  it('records an away forfeit, reloading even on failure', () => {
    load(page());
    press('Away Team Forfeit');
    expect(text()).toContain('The away team forfeits and the home team wins.');
    press('Confirm');

    const req = httpMock.expectOne('/api/season-matches/7/forfeit/away');
    expect(req.request.method).toBe('POST');
    req.flush('no', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/season-matches/7').flush(page());
    expect(component.submitting()).toBeFalse();
    fixture.detectChanges();
    expect(component.actionError()).toBe('no');
    expect(text()).toContain('no');
  });

  it('records a forfeit by both teams once confirmed', () => {
    load(page({ isPlayoff: true, bracketInfo: bracket() }));
    press('Both Teams Forfeit');
    expect(text()).toContain('Both teams forfeit and take a loss.');
    press('Confirm');

    const req = httpMock.expectOne('/api/season-matches/7/forfeit/double');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    httpMock.expectOne('/api/season-matches/7').flush(page());
  });

  it('records nothing when a forfeit is cancelled', () => {
    load(page());
    press('Both Teams Forfeit');
    press('Cancel');

    httpMock.expectNone('/api/season-matches/7/forfeit/double');
    expect(component.confirming()).toBeNull();
    expect(button('Both Teams Forfeit')).toBeDefined();
  });

  it('confirming does nothing when no action is pending', () => {
    load(page());
    component.confirmAction();
    expect(component.submitting()).toBeFalse();
  });

  it('offers no forfeit for the grand final or its sudden death, only reporting', () => {
    load(page({ isPlayoff: true, bracketInfo: bracket({ isGrandFinal: true }) }));

    expect(text()).not.toContain('Forfeit');
    expect(text()).toContain('Enter Match ID');
  });

  it('undoes a result once confirmed', () => {
    load(page({ ...forfeited }));
    press('Undo Result');

    httpMock.expectNone('/api/season-matches/7/result/undo');
    expect(text()).toContain('This match goes back to undecided');
    press('Confirm');

    const req = httpMock.expectOne('/api/season-matches/7/result/undo');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    httpMock.expectOne('/api/season-matches/7').flush(page());
    fixture.detectChanges();
    expect(button('Undo Result')).toBeUndefined();
    expect(button('Home Team Forfeit')).toBeDefined();
  });

  it('shows why the server would not undo a result', () => {
    load(page({ ...wonByGames }));
    press('Undo Result');
    press('Confirm');

    httpMock.expectOne('/api/season-matches/7/result/undo').flush('Undo that match first.', { status: 400, statusText: 'Bad Request' });
    httpMock.expectOne('/api/season-matches/7').flush(page({ ...wonByGames }));
    fixture.detectChanges();
    expect(text()).toContain('Undo that match first.');
  });

  it('offers undo for reported games of an undecided series', () => {
    load(page({ reportedGames: [{ seriesGameID: 501, matchNumber: 1, matchID: 'g-1' }], canUndo: true }));
    expect(button('Undo Result')).toBeDefined();
    expect(button('Home Team Forfeit')).toBeDefined();
  });

  it('offers no undo without a result, or for a match with a BYE', () => {
    load(page());
    expect(button('Undo Result')).toBeUndefined();

    component.seasonMatch.set(page({ isPlayoff: true, ...awayBye }));
    fixture.detectChanges();
    expect(button('Undo Result')).toBeUndefined();
    expect(text()).toContain('This match has been completed.');
  });

  it("takes decided and Undo from the server (decidedBy, canUndo), never from the results or the games", () => {
    // Games reported, but the server says there is nothing Undo may take back
    load(page({ reportedGames: [{ seriesGameID: 501, matchNumber: 1, matchID: 'g-1' }], canUndo: false }));
    expect(button('Undo Result')).toBeUndefined();
    expect(component.isDecided()).toBeFalse();

    component.seasonMatch.set(page({ decidedBy: 'Forfeit', canUndo: true }));
    fixture.detectChanges();
    expect(component.isDecided()).toBeTrue();
    expect(text()).toContain('This match has been completed.');
    expect(text()).toContain('Match report');
    expect(button('Undo Result')).toBeDefined();
  });

  // ---------- mode: one thing at a time (idle, confirm, placeReport, submitting) ----------

  const withGames = () => page({ reportedGames: [manualGame()] });
  const slotChoice = () => (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Is this a new game, or does it replace one?"]');

  it('is idle until the commissioner asks for something', () => {
    load(page());
    expect(component.mode()).toEqual({ kind: 'idle' });
    expect([component.confirming(), component.placingReport(), component.submitting()]).toEqual([null, null, false]);
  });

  it('idle to confirm, and back on Cancel', () => {
    load(page());
    component.requestAction('AwayForfeit');
    expect(component.mode()).toEqual({ kind: 'confirm', action: 'AwayForfeit' });
    component.cancel();
    expect(component.mode()).toEqual({ kind: 'idle' });
  });

  it('idle to placeReport, and back on Cancel', () => {
    load(withGames(), [possible]);
    press('SUBMIT');
    expect(component.mode()).toEqual({ kind: 'placeReport', matchID: 'abc-123' });
    expect(slotChoice()).not.toBeNull();
    Array.from(slotChoice()!.querySelectorAll('button')).find(b => b.textContent!.trim() === 'Cancel')!.click();
    fixture.detectChanges();
    expect(component.mode()).toEqual({ kind: 'idle' });
    expect(slotChoice()).toBeNull();
  });

  it('asking for an action drops a game waiting to be placed (it was left open before), and the other way round', () => {
    load(withGames(), [possible]);
    press('SUBMIT');
    press('Home Team Forfeit');
    expect(component.mode()).toEqual({ kind: 'confirm', action: 'HomeForfeit' });
    expect(slotChoice()).toBeNull();
    expect(text()).toContain('The home team forfeits and the away team wins.');

    component.onSubmit('abc-123');
    fixture.detectChanges();
    expect(component.mode()).toEqual({ kind: 'placeReport', matchID: 'abc-123' });
    expect(text()).not.toContain('The home team forfeits');
    httpMock.expectNone(r => r.method === 'POST');
  });

  it('placeReport to submitting to idle, reporting the game where it was placed', () => {
    load(withGames(), [possible]);
    component.onSubmit('abc-123');
    component.placeReport(51);
    expect(component.mode()).toEqual({ kind: 'submitting' });
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc-123/report?replaces=51' }).flush('ok');
    expect(component.mode()).toEqual({ kind: 'submitting' });
    httpMock.expectOne('/api/season-matches/7').flush(withGames());
    expect(component.mode()).toEqual({ kind: 'idle' });
  });

  it('places nothing when no game is waiting to be placed', () => {
    load(withGames());
    component.placeReport('new');
    component.requestAction('DoubleForfeit');
    component.placeReport('new');
    expect(component.mode()).toEqual({ kind: 'confirm', action: 'DoubleForfeit' });
    httpMock.expectNone(r => r.method === 'POST');
  });

  it('while submitting, asks for, cancels and sends nothing else, and every tool waits', () => {
    load(withGames(), [possible]);
    component.requestAction('UndoResult');
    component.confirmAction();
    const req = httpMock.expectOne('/api/season-matches/7/result/undo');
    fixture.detectChanges();
    expect(component.mode()).toEqual({ kind: 'submitting' });
    expect(button('SUBMIT')!.disabled).toBeTrue();
    expect(button('Home Team Forfeit')!.disabled).toBeTrue();
    expect(manualEntry().blocked()).toBeTrue();
    expect(button('Edit')).toBeUndefined();

    component.requestAction('HomeForfeit');
    component.onSubmit('abc-123');
    component.cancel();
    component.confirmAction();
    expect(component.mode()).toEqual({ kind: 'submitting' });

    req.flush(null);
    httpMock.expectOne('/api/season-matches/7').flush(withGames());
    fixture.detectChanges();
    expect(component.mode()).toEqual({ kind: 'idle' });
    expect(manualEntry().blocked()).toBeFalse();
    expect(button('Edit')).toBeDefined();
  });

  it('waits while manual entry is saving a game: one thing is sent at a time', () => {
    load(page(), [possible]);
    manualEntry().saving.set(true);
    fixture.detectChanges();
    expect(button('SUBMIT')!.disabled).toBeTrue();
    expect(button('Home Team Forfeit')!.disabled).toBeTrue();

    manualEntry().saving.set(false);
    fixture.detectChanges();
    expect(button('Home Team Forfeit')!.disabled).toBeFalse();

    // Nor is anything asked for or sent meanwhile, even a Confirm left open
    component.requestAction('HomeForfeit');
    manualEntry().saving.set(true);
    component.confirmAction();
    expect(component.mode()).toEqual({ kind: 'confirm', action: 'HomeForfeit' });
    component.cancel();
    component.requestAction('HomeForfeit');
    component.onSubmit('abc-123');
    expect(component.mode()).toEqual({ kind: 'idle' });
    httpMock.expectNone(r => r.method === 'POST');
  });

  // ---------- the page and manual entry never both wait for the commissioner ----------

  it('asking for an action or a slot, or sending, drops what manual entry had waiting', () => {
    load(withGames(), [possible]);
    component.requestAction('HomeForfeit');
    expect(manualEntry().cancel).toHaveBeenCalledTimes(1);
    component.onSubmit('abc-123');
    expect(manualEntry().cancel).toHaveBeenCalledTimes(2);
    component.placeReport('new');
    expect(manualEntry().cancel).toHaveBeenCalledTimes(3);
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc-123/report' }).flush('ok');
    httpMock.expectOne('/api/season-matches/7').flush(withGames());
  });

  it('drops its Confirm or slot choice when manual entry starts something', () => {
    load(withGames(), [possible]);
    press('Home Team Forfeit');
    manualEntry().started.emit();
    fixture.detectChanges();
    expect(component.mode()).toEqual({ kind: 'idle' });
    expect(text()).not.toContain('The home team forfeits');

    press('SUBMIT');
    expect(slotChoice()).not.toBeNull();
    manualEntry().started.emit();
    fixture.detectChanges();
    expect(slotChoice()).toBeNull();
  });

  it('drops a Confirm left open when manual entry changes the games and the page reloads', () => {
    load(page());
    component.requestAction('HomeForfeit');
    manualEntry().changed.emit();
    httpMock.expectOne('/api/season-matches/7').flush(page({ ...wonByGames, reportedGames: [manualGame()], canUndo: true }));
    fixture.detectChanges();
    expect(component.confirming()).toBeNull();
    expect(text()).toContain('This match has been completed.');
  });

  // ---------- focus: never left on a control that is gone ----------

  const heading = () => (fixture.nativeElement as HTMLElement).querySelector('#lobby-report') as HTMLElement;
  const admin = () => (fixture.nativeElement as HTMLElement).querySelector('.lobby-admin') as HTMLElement;
  const focused = () => document.activeElement;
  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }
  async function pressFocused(control: HTMLButtonElement): Promise<void> {
    control.focus();
    control.click();
    await settle();
  }

  it('focuses the Report Game heading when a Confirm opens or is sent, and the action again on Cancel', async () => {
    load(page());
    expect(heading().getAttribute('tabindex')).toBe('-1');
    expect(admin().getAttribute('aria-busy')).toBe('false');

    await pressFocused(button('Away Team Forfeit')!);
    expect(button('Away Team Forfeit')).toBeUndefined();
    expect(focused()).toBe(heading());

    await pressFocused(button('Cancel')!);
    expect(focused()).toBe(button('Away Team Forfeit')!);

    await pressFocused(button('Away Team Forfeit')!);
    await pressFocused(button('Confirm')!);
    expect(focused()).toBe(heading());
    expect(admin().getAttribute('aria-busy')).toBe('true');

    httpMock.expectOne('/api/season-matches/7/forfeit/away').flush(null);
    httpMock.expectOne('/api/season-matches/7').flush(page());
    await settle();
    expect(admin().getAttribute('aria-busy')).toBe('false');
  });

  it("a slot choice: Cancel goes back to the game's SUBMIT, a choice or an action asked for meanwhile to the heading", async () => {
    load(withGames(), [possible]);
    const cancelSlot = () => Array.from(slotChoice()!.querySelectorAll('button')).find(b => b.textContent!.trim() === 'Cancel') as HTMLButtonElement;

    await pressFocused(button('SUBMIT')!);
    await pressFocused(cancelSlot());
    expect(slotChoice()).toBeNull();
    expect(focused()).toBe(button('SUBMIT')!);

    // An action asked for while the slot choice is open (focus in it): the choice is gone
    await pressFocused(button('SUBMIT')!);
    button('A new game (game 2)')!.focus();
    await pressFocused(button('Home Team Forfeit')!);
    expect(slotChoice()).toBeNull();
    expect(focused()).toBe(heading());
    await pressFocused(button('Cancel')!);
    expect(focused()).toBe(button('Home Team Forfeit')!);

    await pressFocused(button('SUBMIT')!);
    await pressFocused(button('Replaces game 1 (entered by hand)')!);
    expect(focused()).toBe(heading());
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/abc-123/report?replaces=51' }).flush('ok');
    httpMock.expectOne('/api/season-matches/7').flush(withGames());
  });

  it('leaves focus with manual entry when it drops what the page had waiting', async () => {
    load(page());
    await pressFocused(button('Home Team Forfeit')!);
    const elsewhere = button('Pull recent games')!;
    elsewhere.focus();
    manualEntry().started.emit();
    await settle();
    expect(focused()).toBe(elsewhere);
    httpMock.expectNone(r => r.method === 'POST');
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

/** The page with the real manual entry: the two never both wait for the commissioner. */
describe('SeasonMatchComponent with manual entry', () => {
  let fixture: ComponentFixture<SeasonMatchComponent>;
  let component: SeasonMatchComponent;
  let httpMock: HttpTestingController;

  const game: ReportedGameDto = {
    seriesGameID: 51, matchNumber: 1, matchID: null, recordedGameID: 41, homeScore: 5, awayScore: 2, homePlayers: [], awayPlayers: [],
    isManual: true, gameVersionID: 1, gameVersionName: 'Halo 3', map: 'Foundry', gametype: null, playedAt: null,
  };
  /** The page as these tests want it: a best of one, not imported. */
  const manualPage = (overrides: Partial<SeasonMatchPageDto> = {}) => page({ bestOf: 1, isImported: false, ...overrides });
  /** Won by its one game, entered by hand. */
  const decidedByIt = () => manualPage({ ...wonByGames, homeTeamScore: 1, awayTeamScore: 0, reportedGames: [game] });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { isEventOrganizer: signal(true), isPlayer: signal(false) } }
      ]
    }).overrideComponent(SeasonMatchComponent, { remove: { imports: [VodListComponent] }, add: { imports: [StubVodListComponent] } }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonMatchComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function load(dto: SeasonMatchPageDto): void {
    loadPage(httpMock, fixture, dto);
    httpMock.match(r => /^\/api\/teams\/\w+$/.test(r.url)).forEach(r => r.flush(null));
    fixture.detectChanges();
    httpMock.expectOne('/api/game-versions').flush([{ gameVersionID: 1, name: 'Halo 3' }]);
    fixture.detectChanges();
  }

  const manual = () => fixture.debugElement.query(By.directive(ManualGamesComponent)).componentInstance as ManualGamesComponent;
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
    .find(b => b.textContent!.trim() === label) as HTMLButtonElement | undefined;
  function press(label: string): void {
    button(label)!.click();
    fixture.detectChanges();
  }

  it('Undo open, then the last game unreported by hand: the Undo panel is gone', () => {
    load(decidedByIt());
    press('Undo Result');
    expect(text()).toContain('This match goes back to undecided');

    press('Unreport');
    expect(component.confirming()).toBeNull();
    expect(text()).not.toContain('This match goes back to undecided');
    press('Confirm');
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/games/51/remove' }).flush(null);
    httpMock.expectOne('/api/season-matches/7').flush(manualPage());
    fixture.detectChanges();

    expect(component.mode()).toEqual({ kind: 'idle' });
    expect(manual().idle()).toBeTrue();
    expect(text()).not.toContain('This match goes back to undecided');
    expect(button('Undo Result')).toBeUndefined();
    expect(button('Home Team Forfeit')).toBeDefined();
  });

  it('a forfeit Confirm open while a game entered by hand decides the series: the Confirm is gone, and the match completed', () => {
    load(manualPage());
    press('Home Team Forfeit');
    expect(text()).toContain('The home team forfeits and the away team wins.');

    press('Enter a game by hand');
    expect(component.confirming()).toBeNull();
    expect(text()).not.toContain('The home team forfeits');
    manual().form.homeTeamScore = 5;
    manual().form.awayTeamScore = 2;
    manual().save();
    httpMock.expectOne({ method: 'POST', url: '/api/season-matches/7/manual-games' }).flush(41);
    httpMock.expectOne('/api/season-matches/7').flush(decidedByIt());
    fixture.detectChanges();

    expect(component.confirming()).toBeNull();
    expect(text()).not.toContain('The home team forfeits');
    expect(text()).toContain('This match has been completed.');
  });

  it('a game in the form, then a forfeit asked for: the form is closed', () => {
    load(manualPage());
    press('Enter a game by hand');
    expect(manual().editing()).toBe('new');

    press('Away Team Forfeit');
    expect(manual().editing()).toBeNull();
    expect(component.confirming()).toBe('AwayForfeit');
    httpMock.expectNone(r => r.method === 'POST');
  });
});

describe('SeasonMatchComponent lobby', () => {
  let fixture: ComponentFixture<SeasonMatchComponent>;
  let httpMock: HttpTestingController;

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

  const load = (dto: SeasonMatchPageDto): void => loadPage(httpMock, fixture, dto);
  const el = () => fixture.nativeElement as HTMLElement;
  const roster = (side: 'home' | 'away') => Array.from(el().querySelectorAll(`.lobby-team--${side} .lobby-player`))
    .map(p => Array.from(p.children).map(c => c.textContent!.trim()).join(' '));

  it("lists each side's players from its team, marking the captain", () => {
    load(page());
    httpMock.expectOne('/api/teams/10').flush(team('Home', 'Grif', 'Simmons'));
    httpMock.expectOne('/api/teams/20').flush(team('Away', 'Tucker', 'Caboose'));
    fixture.detectChanges();

    expect(roster('home')).toEqual(['G Grif Captain', 'S Simmons']);
    expect(roster('away')).toEqual(['Captain Tucker T', 'Caboose C']);
  });

  it('shows players without a gamertag (no Xbox account linked) without breaking the page', () => {
    load(page());
    httpMock.expectOne('/api/teams/10').flush({ ...team('Home', 'Grif', 'x'), players: [{ teamPlayerID: 3, userID: 3, gamertag: null, draftCaptainOrder: 1, draftRound: null }] });
    httpMock.expectOne('/api/teams/20').flush({ ...team('Away', 'Tucker', 'x'), players: [{ teamPlayerID: 4, userID: 4, gamertag: null, draftCaptainOrder: null, draftRound: 1 }] });
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
    httpMock.expectOne('/api/teams/10').flush(team('Home', 'Grif', 'Simmons'));
    fixture.detectChanges();
    expect(pageEl().classList).toContain('hud-waiting');
    httpMock.expectOne('/api/teams/20').flush(team('Away', 'Tucker', 'Caboose'));
    fixture.detectChanges();
    expect(pageEl().classList).not.toContain('hud-waiting');
  });

  it('shows the page for a match that does not exist, or that could not be loaded', () => {
    httpMock.expectOne('/api/season-matches/7').flush('x', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/season-matches/7/possible-matches').flush([]);
    httpMock.expectOne('/api/season-matches/7/match-pull').flush(idlePull);
    fixture.detectChanges();
    expect(fixture.componentInstance.ready()).toBeTrue();
  });

  it('does not reload the rosters when the match page is reloaded for the same teams', () => {
    load(page());
    httpMock.expectOne('/api/teams/10').flush(team('Home', 'Grif', 'Simmons'));
    httpMock.expectOne('/api/teams/20').flush(null);
    fixture.componentInstance.getPageDto();
    httpMock.expectOne('/api/season-matches/7').flush(page({ homeTeamScore: 1, awayTeamScore: 0 }));
    fixture.detectChanges();

    httpMock.expectNone(r => /^\/api\/teams\/\w+$/.test(r.url));
    expect(roster('home')).toHaveSize(2);
    expect(roster('away')).toEqual([]);
  });

  it('leaves a roster empty when it cannot be loaded, and has none for a BYE', () => {
    load(page({ isPlayoff: true, ...awayBye }));
    httpMock.expectOne('/api/teams/10').flush('x', { status: 500, statusText: 'Server Error' });
    httpMock.expectNone('/api/teams/null');
    fixture.detectChanges();

    expect(roster('home')).toEqual([]);
    expect(roster('away')).toEqual([]);
  });

  it('is a lobby until a result is in, then a match report with the score', () => {
    load(page());
    httpMock.match(() => true).forEach(r => r.flush(null));
    expect(el().querySelector('h1')!.textContent).toBe('Pre-game lobby');
    expect(el().querySelector('.lobby-score__value')!.textContent!.replace(/\s+/g, '')).toBe('-:-');

    fixture.componentInstance.seasonMatch.set(page({ homeTeamScore: 2, awayTeamScore: 1, ...wonByGames }));
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
          seriesGameID: 1,
          matchNumber: 1,
          matchID: '123',
          homePlayers: [{ userID: 1, gamertag: 'Solo', kills: 5, deaths: 2, assists: 0, bombScores: 1, score: null }],
          awayPlayers: [],
        }
      ]
    }));
    fixture.detectChanges();
    expect(comp.availableTabs()).toHaveSize(1);
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

    httpMock.expectOne('/api/season-matches/7');
    httpMock.expectNone('/api/season-matches/7/possible-matches');
    // The page drawn on the server says the search is under way; the browser does it
    expect(fixture.componentInstance.possibleMatchesState()).toBe('loading');
    httpMock.verify();
  });
});

describe('describeSeasonMatch', () => {
  it('gives the series score and winner once its games decided it', () => {
    expect(describeSeasonMatch(page({ homeTeamScore: 1, awayTeamScore: 2, homeTeamResult: 'Loss', awayTeamResult: 'Won', decidedBy: 'Games' }))).toEqual({
      title: 'Home 1–2 Away',
      description: 'Season 3, best of 3: Away beat Home 2–1. Games, scores and player stats.',
    });
  });

  it('gives a forfeit\'s winner, without the games\' score even when some were played', () => {
    expect(describeSeasonMatch(page({ homeTeamResult: 'Won', awayTeamResult: 'Forfeit', decidedBy: 'Forfeit', isPlayoff: true }))).toEqual({
      title: 'Home vs Away',
      description: 'Season 3 playoffs, best of 3: Away forfeited to Home. Games, scores and player stats.',
    });
    // Home led 1–0, then forfeited
    expect(describeSeasonMatch(page({ homeTeamScore: 1, awayTeamScore: 0, homeTeamResult: 'Forfeit', awayTeamResult: 'Won', decidedBy: 'Forfeit' }))).toEqual({
      title: 'Home vs Away',
      description: 'Season 3, best of 3: Home forfeited to Away. Games, scores and player stats.',
    });
  });

  it('says when both teams forfeited, not when it is scheduled', () => {
    expect(describeSeasonMatch(page({ homeTeamResult: 'Forfeit', awayTeamResult: 'Forfeit', decidedBy: 'Forfeit', scheduledTime: DateTime.fromISO('2026-11-20T01:00:00Z') })).description)
      .toBe('Season 3, best of 3: both Home and Away forfeited. Games, scores and player stats.');
  });

  it('gives a BYE as the team that advanced', () => {
    expect(describeSeasonMatch(page({ awayTeamName: null, awayTeamID: null, homeTeamResult: 'Won', awayTeamResult: 'Bye', decidedBy: 'Bye' }))).toEqual({
      title: 'Home (bye)',
      description: 'Season 3, best of 3: Home advanced with a bye. Games, scores and player stats.',
    });
  });

  it('gives when an undecided match is scheduled (the league\'s day), and TBD for a team not set yet', () => {
    expect(describeSeasonMatch(page({ awayTeamName: null, scheduledTime: DateTime.fromISO('2026-11-20T01:00:00Z') }))).toEqual({
      title: 'Home vs TBD',
      description: 'Season 3, best of 3: Home vs TBD, scheduled for Nov 19, 2026. Games, scores and player stats.',
    });
    expect(describeSeasonMatch(page()).description).toBe('Season 3, best of 3: Home vs Away. Games, scores and player stats.');
  });
});
