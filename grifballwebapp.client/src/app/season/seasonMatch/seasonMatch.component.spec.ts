import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeasonMatchComponent } from './seasonMatch.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { JWT_OPTIONS, JwtHelperService } from '@auth0/angular-jwt';
import { ActivatedRoute, provideRouter } from '@angular/router';
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
        { provide: JWT_OPTIONS, useValue: {} },
        JwtHelperService,
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
    expect(text()).toContain('Scheduled Date:');
    expect(text()).not.toContain('Scheduled Date: 1');
  });

  it('shows the regular season text when the match has no bracket info', () => {
    load(page());

    expect(text()).toContain('Regular Season Match');
    expect(links()).toEqual([]);
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

  it('formats the scheduled time once loaded', () => {
    load(page({ scheduledTime: '2026-01-02T03:04:00' as unknown as SeasonMatchPageDto['scheduledTime'] }));

    expect(text()).toMatch(/Scheduled Date: 1\/2\/26, 3:04/);
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

  const player = (gamertag: string, score: number): PossiblePlayerDto =>
    ({ xboxUserID: score, gamertag, score, kills: score + 1, deaths: score + 2, isOnTeam: true });

  const possible: PossibleMatchDto = {
    matchID: 'abc-123',
    homeTeam: { teamID: 10, score: 50, outcome: 2, players: [player('Grif', 11)] },
    awayTeam: { teamID: 20, score: 49, outcome: 3, players: [player('Tucker', 7)] },
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
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
    .find(b => b.textContent!.trim() === label) as HTMLButtonElement | undefined;

  it('lists reported games', () => {
    load(page({ reportedGames: [{ matchNumber: 1, matchID: 'g-1' }, { matchNumber: 2, matchID: 'g-2' }] }));
    expect(text()).toContain('Game 1 - g-1');
    expect(text()).toContain('Game 2 - g-2');
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

  it('records a home forfeit', () => {
    load(page());
    button('Home Team Forfeit')!.click();
    expect(component.isSubmittingMatch()).toBeTrue();

    httpMock.expectOne('/api/SeasonMatch/HomeForfeit/7').flush('ok');
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    expect(component.isSubmittingMatch()).toBeFalse();
  });

  it('records an away forfeit, reloading even on failure', () => {
    load(page());
    button('Away Team Forfeit')!.click();

    httpMock.expectOne('/api/SeasonMatch/AwayForfeit/7').flush('no', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/SeasonMatch/GetSeasonMatchPage/7').flush(page());
    expect(component.isSubmittingMatch()).toBeFalse();
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
