import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LateLeagueComponent } from './lateLeague.component';
import type { MatchmakingPlayerStatDto, MatchmakingQueueDto, MatchmakingSeasonDto } from '../api/dtos/lateLeagueDto';

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

  const lists = (queue: MatchmakingQueueDto | null, seasons: MatchmakingSeasonDto[] | null, fail = { queue: false, seasons: false }) => {
    const error = { status: 500, statusText: 'Server Error' };
    const q = http.expectOne('/api/LateLeague/GetQueue');
    if (fail.queue) q.flush('no', error); else q.flush(queue);
    const s = http.expectOne('/api/LateLeague/GetSeasons');
    if (fail.seasons) s.flush('no', error); else s.flush(seasons);
    fixture.detectChanges();
  };
  const section = (id: string) => el().querySelector(`[aria-labelledby="${id}"]`)!;
  const sectionRows = (id: string) => Array.from(section(id).querySelectorAll('tbody tr')).map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  it('leads with the Discord queue\'s players, then the archive\'s seasons, each linking to its page', () => {
    fixture.detectChanges();
    lists(
      { gameCount: 25, players: [player(3, 'Queue Regular', { matchmakingSeasonID: null, matchmakingSeasonName: null, gameVersionName: 'Halo Infinite' })] },
      [season(8), season(7, { gameVersionName: null, gameCount: 0, playerCount: 9 })]);

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Late League');
    expect(Array.from(el().querySelectorAll('h2')).map(h => h.textContent!.trim())).toEqual(['Discord queue', 'Past seasons']);
    expect(section('late-league-queue').querySelector('.facts')!.textContent).toContain('25 games reported');
    expect(sectionRows('late-league-queue')).toEqual([['1', 'Queue Regular', 'Halo Infinite', '10', '6-4', '60%', '12', '50', '40', '+10', '1.25', '7', 'Games']]);
    expect(sectionRows('late-league-seasons')).toEqual([
      ['Late League Season 8 (Halo 3)', 'Halo 3', 'Mar 2020 – Sep 2020', '40', '12'],
      ['Late League Season 7 (Halo 3)', '-', 'Mar 2020 – Sep 2020', '0', '9'],
    ]);
    expect(section('late-league-seasons').querySelector('tbody a')!.getAttribute('href')).toBe('/late-league/8');
  });

  it('says when the queue or the archive has nothing yet', () => {
    fixture.detectChanges();
    lists({ gameCount: 0, players: [] }, []);
    expect(section('late-league-queue').textContent).toContain('No queue games have been reported yet.');
    expect(section('late-league-seasons').textContent).toContain('No Late League seasons from the archive.');
  });

  it('shows what loaded when the other part cannot be', () => {
    fixture.detectChanges();
    lists(null, [season(8)], { queue: true, seasons: false });
    expect(section('late-league-queue').textContent).toContain('The queue\'s stats couldn\'t be loaded.');
    expect(sectionRows('late-league-seasons').length).toBe(1);

    const other = TestBed.createComponent(LateLeagueComponent);
    other.detectChanges();
    http.expectOne('/api/LateLeague/GetQueue').flush({ gameCount: 0, players: [] });
    http.expectOne('/api/LateLeague/GetSeasons').flush('no', { status: 500, statusText: 'Server Error' });
    other.detectChanges();
    expect((other.nativeElement as HTMLElement).textContent).toContain('The past seasons couldn\'t be loaded.');
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
      ['1', 'Alpha One', 'Halo 3', '10', '6-4', '60%', '12', '50', '40', '+10', '1.25', '7', 'Games'],
      ['2', 'Bravo Two', 'Halo 3', '10', '6-4', '-', '12', '50', '40', '-3', '-', '7', 'Sheet'],
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
