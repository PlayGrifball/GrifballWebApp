import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { SeasonListItemDto, SeasonsComponent } from './seasons.component';

function season(seasonID: number, changes: Partial<SeasonListItemDto> = {}): SeasonListItemDto {
  return {
    seasonID, name: `Season ${seasonID}`, start: '2025-01-01T00:00:00Z', end: '2025-03-01T00:00:00Z', year: 2025, isCurrent: false,
    leagueID: 1, leagueName: 'AGLA', leagueSlug: 'agla', gameVersionID: 6, gameVersionName: 'Halo Infinite', gameVersionSlug: 'infinite',
    isHistorical: false, teamCount: 4, playerCount: 20, champions: [], ...changes,
  };
}

describe('SeasonsComponent', () => {
  let fixture: ComponentFixture<SeasonsComponent>;
  let http: HttpTestingController;
  const all = [
    season(12, { name: 'Autumn Brawl', isCurrent: true }),
    season(5, { name: 'Summer League 2010', year: 2010, gameVersionID: 1, gameVersionName: 'Halo 3', isHistorical: true,
      historicalSourceUrl: 'https://docs.google.com/spreadsheets/d/x',
      champions: [{ tier: 'Pro', teamID: 41, teamName: 'Relentless', roster: [] }] }),
    season(7, { name: 'Late League Season 8', leagueID: 2, leagueName: 'Late League', gameVersionID: null, gameVersionName: null }),
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonsComponent);
    fixture.detectChanges();
    http.expectOne(r => r.url === '/api/Season/GetSeasons' && r.params.get('pageSize') === '250' && !r.params.has('leagueID'))
      .flush({ totalCount: all.length, results: all });
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const names = () => Array.from(el().querySelectorAll('.season-card__name')).map(a => a.textContent!.trim());

  it('lists every season, linked, with its current badge, champions and spreadsheet', () => {
    expect(names()).toEqual(['Autumn Brawl', 'Summer League 2010', 'Late League Season 8']);
    const cards = el().querySelectorAll('.season-card');
    expect(cards[0].textContent).toContain('Current');
    expect(Array.from(cards[1].querySelectorAll('a')).map(a => a.getAttribute('href')))
      .toEqual(['/season/5', '/season/5/team/41', 'https://docs.google.com/spreadsheets/d/x']);
  });

  it('offers the leagues and games there are seasons of', () => {
    const [leagues, games] = Array.from(el().querySelectorAll('select')).map(s => Array.from(s.options).map(o => o.textContent!.trim()));
    expect(leagues).toEqual(['All leagues', 'AGLA', 'Late League']);
    expect(games).toEqual(['All games', 'Halo 3', 'Halo Infinite']);
  });

  it('asks for the seasons of the chosen league and game, and all again', () => {
    const [league, game] = Array.from(el().querySelectorAll('select'));
    league.value = '2';
    league.dispatchEvent(new Event('change'));
    http.expectOne(r => r.params.get('leagueID') === '2' && !r.params.has('gameVersionID')).flush({ totalCount: 1, results: [all[2]] });
    game.value = '6';
    game.dispatchEvent(new Event('change'));
    http.expectOne(r => r.params.get('leagueID') === '2' && r.params.get('gameVersionID') === '6').flush({ totalCount: 0, results: [] });
    fixture.detectChanges();
    expect(names()).toEqual([]);
    expect(el().textContent).toContain('No seasons match.');
    expect(el().querySelectorAll('select')[0].options.length).withContext('The options stay those of every season').toBe(3);

    league.value = '';
    league.dispatchEvent(new Event('change'));
    http.expectOne(r => !r.params.has('leagueID') && r.params.get('gameVersionID') === '6').flush({ totalCount: 1, results: [all[0]] });
    fixture.detectChanges();
    expect(names()).toEqual(['Autumn Brawl']);
  });

  it('searches by season name or champion', () => {
    const input = el().querySelector('input')!;
    input.value = 'relent';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(names()).toEqual(['Summer League 2010']);
  });
});

describe('SeasonsComponent failing', () => {
  it('shows the page, empty, when the list cannot be loaded', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SeasonsComponent);
    fixture.detectChanges();
    http.expectOne(r => r.url === '/api/Season/GetSeasons').flush('', { status: 500, statusText: 'down' });
    fixture.detectChanges();
    expect(fixture.componentInstance.loaded()).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No seasons match.');
  });
});
