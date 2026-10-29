import { DateTime } from 'luxon';
import { DatePipe } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeasonMatchComponent } from './seasonMatch.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { AccountService } from '../../account.service';
import { BracketInfoDto, SeasonMatchPageDto } from './seasonMatchPageDto';
import { PossibleMatchDto, PossiblePlayerDto } from './possibleMatchDto';

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
    }).compileComponents();

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
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonMatchComponent);
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();
  });

  function load(dto: SeasonMatchPageDto): void {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(dto);
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function links(): { text: string, href: string | null }[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a.block'))
      .map(a => ({ text: a.textContent?.trim() ?? '', href: a.getAttribute('href') }));
  }

  it('shows the regular season text, no bracket links and no date before the page loads', () => {
    expect(text()).toContain('Regular Season Match');
    expect(links()).toEqual([]);
    expect(text()).toContain('Not scheduled');
    expect(text()).not.toContain('Starts');
  });

  it('shows the empty page for an unknown match (the API answers 204 with no body)', () => {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(null, { status: 204, statusText: 'No Content' });
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
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
    scheduledTime: null, bestOf: 3, reportedGames: [], bracketInfo: null, activeRescheduleRequestId: null,
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
    }).compileComponents();

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
    fixture.detectChanges();
    // The rosters are not under test here.
    httpMock.match(r => r.url.startsWith('/api/Team/Team/')).forEach(r => r.flush(null));
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
    .find(b => b.textContent!.trim() === label) as HTMLButtonElement | undefined;

  it('lists reported games', () => {
    load(page({ reportedGames: [{ matchNumber: 1, matchID: 'g-1' }, { matchNumber: 2, matchID: 'g-2' }] }));
    const games = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.lobby-games li')).map(li => Array.from(li.children).map(c => c.textContent!.trim()).join(' '));
    expect(games).toEqual(['Game 1 g-1', 'Game 2 g-2']);
    expect(text()).not.toContain('There are no reported games');
  });

  it('shows an active reschedule request instead of the reschedule button', () => {
    load(page({ activeRescheduleRequestId: 88 }));
    expect(text()).toContain('Request ID: 88');
    expect(button('Reschedule')).toBeUndefined();
  });

  it('offers players a reschedule link when there is no active request', () => {
    isEventOrganizer.set(false);
    isPlayer.set(true);
    load(page());
    expect(button('Reschedule')).toBeDefined();
    expect(text()).not.toContain('Report Game');
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

    httpMock.expectOne('/api/SeasonMatch/ReportMatch/7/abc-123').flush('ok');
    expect(component.isSubmittingMatch()).toBeFalse();
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page({ homeTeamResult: 'Won', awayTeamResult: 'Loss' }));
    fixture.detectChanges();
    expect(text()).toContain('This match has been completed.');
  });

  it('clears the submitting state and reloads even when reporting fails', () => {
    load(page());
    component.onSubmit('bad');
    httpMock.expectOne('/api/SeasonMatch/ReportMatch/7/bad').flush('no', { status: 400, statusText: 'Bad Request' });

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
    scheduledTime: null, bestOf: 3, reportedGames: [], bracketInfo: null, activeRescheduleRequestId: null,
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
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonMatchComponent);
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function load(dto: SeasonMatchPageDto): void {
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(dto);
    httpMock.expectOne('/api/SeasonMatch/GetPossibleMatches/7').flush([]);
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
});
