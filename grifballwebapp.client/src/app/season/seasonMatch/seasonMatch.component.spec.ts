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
