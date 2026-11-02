import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LateLeagueComponent } from './lateLeague.component';
import type { MatchmakingPlayerStatDto, MatchmakingSeasonDto } from '../api/dtos/lateLeagueDto';

describe('LateLeagueComponent', () => {
  let fixture: ComponentFixture<LateLeagueComponent>;
  let http: HttpTestingController;
  const season = (id: number, overrides: Partial<MatchmakingSeasonDto> = {}): MatchmakingSeasonDto => ({
    matchmakingSeasonID: id, name: `Late League Season ${id} (Halo 3)`, gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3',
    startsAt: '2020-03-01T00:00:00Z', endsAt: '2020-09-30T00:00:00Z', gameCount: 40, playerCount: 12, isHistorical: true,
    historicalSourceUrl: 'https://docs.google.com/spreadsheets/d/x', ...overrides,
  });
  const player = (userID: number, gamertag: string, overrides: Partial<MatchmakingPlayerStatDto> = {}): MatchmakingPlayerStatDto => ({
    matchmakingSeasonID: 8, matchmakingSeasonName: 'Late League Season 8 (Halo 3)', userID, gamertag, gameVersionID: 1, gameVersionName: 'Halo 3',
    gameVersionSlug: 'halo3', source: 'Matches', gamesPlayed: 10, wins: 6, losses: 4, winPercent: 60, goals: 12, kills: 50, deaths: 40, assists: 5,
    kdRatio: 1.25, kdSpread: 10, carrierKills: 7, ...overrides,
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('tbody tr')).map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LateLeagueComponent);
  });

  afterEach(() => http.verify());

  it('lists the matchmaking seasons, each linking to its page', () => {
    fixture.detectChanges();
    http.expectOne('/api/LateLeague/GetSeasons').flush([season(8), season(7, { gameVersionName: null, gameCount: 0, playerCount: 9 })]);
    fixture.detectChanges();

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Late League');
    expect(rows()).toEqual([
      ['Late League Season 8 (Halo 3)', 'Halo 3', 'Mar 2020 – Sep 2020', '40', '12'],
      ['Late League Season 7 (Halo 3)', '-', 'Mar 2020 – Sep 2020', '0', '9'],
    ]);
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/late-league/8');
  });

  it('says when there are no seasons, or they cannot be loaded', () => {
    fixture.detectChanges();
    http.expectOne('/api/LateLeague/GetSeasons').flush([]);
    fixture.detectChanges();
    expect(el().textContent).toContain('No Late League seasons yet.');

    const failed = TestBed.createComponent(LateLeagueComponent);
    failed.detectChanges();
    http.expectOne('/api/LateLeague/GetSeasons').flush('no', { status: 500, statusText: 'Server Error' });
    failed.detectChanges();
    expect((failed.nativeElement as HTMLElement).textContent).toContain('Late League couldn\'t be loaded.');
  });

  it('shows a season\'s players, linked to their profiles, and where each line comes from', () => {
    fixture.componentRef.setInput('matchmakingSeasonID', '8');
    fixture.detectChanges();
    http.expectOne('/api/LateLeague/GetSeason/8').flush({
      season: season(8),
      players: [player(1, 'Alpha One'), player(2, 'Bravo Two', { source: 'Recorded', winPercent: null, kdRatio: null, kdSpread: -3 })],
    });
    fixture.detectChanges();

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Late League Season 8 (Halo 3)');
    expect(el().querySelector('.facts')!.textContent).toContain('40 games');
    expect(el().querySelector<HTMLAnchorElement>('.facts a')!.href).toBe('https://docs.google.com/spreadsheets/d/x');
    expect(rows()).toEqual([
      ['1', 'Alpha One', '10', '6-4', '60%', '12', '50', '40', '+10', '1.25', '7', 'Games'],
      ['2', 'Bravo Two', '10', '6-4', '-', '12', '50', '40', '-3', '-', '7', 'Sheet'],
    ]);
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/profile/1');
    expect(el().querySelector('.back')!.getAttribute('href')).toBe('/late-league');
  });

  it('says when a season has no players, or cannot be loaded', () => {
    fixture.componentRef.setInput('matchmakingSeasonID', '8');
    fixture.detectChanges();
    http.expectOne('/api/LateLeague/GetSeason/8').flush({ season: season(8, { gameVersionName: null, historicalSourceUrl: null }), players: [] });
    fixture.detectChanges();
    expect(el().textContent).toContain('No player stats were recorded for this season.');
    expect(el().querySelector('.facts a')).toBeNull();

    fixture.componentRef.setInput('matchmakingSeasonID', '9');
    fixture.detectChanges();
    http.expectOne('/api/LateLeague/GetSeason/9').flush('gone', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(el().textContent).toContain('That Late League season couldn\'t be loaded.');
  });
});
