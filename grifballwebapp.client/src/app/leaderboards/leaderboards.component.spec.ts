import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LeaderboardsComponent } from './leaderboards.component';
import { LeaderboardsDto } from '../api/dtos/leaderboardsDto';

describe('LeaderboardsComponent', () => {
  let fixture: ComponentFixture<LeaderboardsComponent>;
  let http: HttpTestingController;
  const leader = (userID: number, gamertag: string, statValue: number, perGame: number | null = 1.5) =>
    ({ rank: 1, userID, gamertag, statValue, gamesPlayed: 30, perGame, seasonsCount: 3 });
  const boards: LeaderboardsDto = {
    ringLeaders: [{ rank: 1, userID: 1, gamertag: 'Legendary', ringsCount: 5, titles: [{ seasonID: 11, seasonName: 'Summer League 2011', tier: 'Pro', teamID: 40 }, { seasonID: 12, seasonName: 'Winter League 2012', tier: 'Pro', teamID: 41 }] }],
    dynasties: [{ rank: 1, teamName: 'Relentless', latestTeamID: 40, latestSeasonID: 10, championshipsCount: 4, seasonsActive: 6, titles: [{ seasonID: 11, seasonName: 'Summer League 2011', tier: 'Pro', teamID: 40 }] }],
    goalLeaders: [leader(1, 'Legendary', 120)],
    killLeaders: [leader(2, 'Killer', 500)],
    gamesPlayedLeaders: [leader(3, 'Ironman', 300, null)],
    carrierStopLeaders: [leader(4, 'Wall', 45)],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LeaderboardsComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('tbody tr')).map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));
  const choose = (title: string) => {
    Array.from(el().querySelectorAll<HTMLButtonElement>('.hud-tab')).find(b => b.textContent!.trim() === title)!.click();
    fixture.detectChanges();
  };

  it('shows each board, linking players to their profiles and teams to their latest title', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();

    expect(rows()).toEqual([['1', 'Legendary', '5', 'Summer League 2011 (Pro), Winter League 2012 (Pro)']]);
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/profile/1');
    expect([...el().querySelectorAll('.board__titles a')].map(a => a.getAttribute('href'))).toEqual(['/season/11', '/season/12']);

    choose('Dynasties');
    expect(rows()[0].slice(0, 4)).toEqual(['1', 'Relentless', '4', '6']);
    expect(el().querySelector('.board__titles a')!.getAttribute('href')).toBe('/season/11/team/40');
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/season/10/team/40');

    choose('Kills');
    expect(rows()).toEqual([['1', 'Killer', '500', '30', '1.50', '3']]);
    choose('Goals');
    expect(rows()[0][1]).toBe('Legendary');
    choose('Carrier stops');
    expect(rows()[0][1]).toBe('Wall');
    choose('Games played');
    expect(rows()).toEqual([['1', 'Ironman', '300', '30', '-', '3']]);
    expect(el().querySelector('[aria-pressed="true"]')!.textContent!.trim()).toBe('Games played');
  });

  it('marks the chosen board pressed, names the table after it and scopes every header to its column', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();
    const pressed = () => Array.from(el().querySelectorAll('.hud-tab')).map(b => b.getAttribute('aria-pressed'));
    expect(pressed()).toEqual(['true', 'false', 'false', 'false', 'false', 'false']);
    expect(el().querySelector('.hud-table-wrap')!.getAttribute('aria-label')).toBe('Championships leaderboard');

    for (const title of ['Championships', 'Dynasties', 'Goals']) {
      choose(title);
      const headers = Array.from(el().querySelectorAll('th'));
      expect(headers.length).toBeGreaterThan(0);
      expect(headers.every(th => th.getAttribute('scope') === 'col')).withContext(title).toBeTrue();
    }
    expect(pressed()).toEqual(['false', 'false', 'true', 'false', 'false', 'false']);
    expect(el().querySelector('.hud-table-wrap')!.getAttribute('aria-label')).toBe('Goals leaderboard');
    expect(fixture.componentInstance.metric()!.leaders[0].perGameText).toBe('1.50');
  });

  it('counts the titles of the chosen format, and every format again', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();
    const select = el().querySelector('select')!;
    expect(select.closest('label')!.querySelector('.hud-label')!.textContent!.trim()).toBe('Titles from');
    expect(Array.from(select.options).map(o => o.textContent!.trim())).toEqual(['All formats', 'League season', 'Tournament']);

    select.value = 'Tournament';
    select.dispatchEvent(new Event('change'));
    http.expectOne(r => r.url === '/api/Leaderboards/GetLeaderboards' && r.params.get('format') === 'Tournament')
      .flush({ ...boards, ringLeaders: [{ userID: 9, gamertag: 'Bracket King', ringsCount: 1, titles: [{ seasonID: 20, seasonName: 'Respawn', tier: 'Open', teamID: 50 }] }] });
    fixture.detectChanges();
    expect(rows()).toEqual([['1', 'Bracket King', '1', 'Respawn (Open)']]);

    select.value = '';
    select.dispatchEvent(new Event('change'));
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
  });

  it('names the game of a per-game total, and lists a player once per game', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush({
      ...boards,
      goalLeaders: [{ ...leader(1, 'Legendary', 120), gameVersionName: 'Halo 3' }, { ...leader(1, 'Legendary', 80), gameVersionName: 'Halo Reach' }],
    });
    fixture.detectChanges();
    choose('Goals');
    expect(rows().map(r => r[1])).toEqual(['LegendaryHalo 3', 'LegendaryHalo Reach']);
    expect(el().querySelector('.board__game')!.textContent).toBe('Halo 3');
  });

  it('says when the leaderboards cannot be loaded', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush('', { status: 500, statusText: 'down' });
    fixture.detectChanges();
    expect(el().textContent).toContain("couldn't be loaded");
  });

  it('searches a player by gamertag or alias, displays the spotlight card and highlights their table row', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();

    const input = el().querySelector<HTMLInputElement>('#playerSearchInput')!;
    input.value = 'Silva';
    input.dispatchEvent(new Event('input'));
    fixture.componentInstance.submitSearch();

    const rankReq = http.expectOne(r => r.url === '/api/Leaderboards/GetPlayerRank' && r.params.get('query') === 'Silva');
    rankReq.flush({
      userID: 1,
      gamertag: 'Legendary',
      matchedAlias: 'The Silvaa',
      pastGamertags: ['The Silvaa', 'Old Silva'],
      isDummyUser: false,
      rings: { rank: 1, totalRanked: 100, statValue: 5, secondaryText: '5 Titles' },
      goals: { rank: 2, totalRanked: 100, statValue: 120, secondaryText: '4.00 / G' },
      kills: { rank: 10, totalRanked: 100, statValue: 350, secondaryText: '1.75 K/D' },
      gamesPlayed: { rank: 15, totalRanked: 100, statValue: 30, secondaryText: '3 Seasons' },
      carrierStops: { rank: 4, totalRanked: 100, statValue: 20, secondaryText: '0.67 / G' },
    });
    fixture.detectChanges();

    const spotlight = el().querySelector('.player-spotlight')!;
    expect(spotlight).not.toBeNull();
    expect(spotlight.querySelector('.player-spotlight__name')!.textContent!.trim()).toBe('Legendary');
    expect(spotlight.querySelector('.player-spotlight__alias-badge')!.textContent).toContain('Matched via past alias: “The Silvaa”');
    expect(spotlight.textContent).toContain('Also known as: The Silvaa, Old Silva');

    // Stats tiles present
    const statCards = spotlight.querySelectorAll('.spotlight-stat');
    expect(statCards.length).toBe(5);
    expect(statCards[0].textContent).toContain('Championships');
    expect(statCards[0].textContent).toContain('#1 of 100');

    // Highlight in the table
    const highlightedRow = el().querySelector('.board__row--highlighted');
    expect(highlightedRow).not.toBeNull();
    expect(highlightedRow!.textContent).toContain('Legendary');

    // Click stat tile to switch tab
    statCards[1].dispatchEvent(new Event('click'));
    fixture.detectChanges();
    expect(fixture.componentInstance.board()).toBe('goals');

    // Clear search
    el().querySelector<HTMLButtonElement>('.player-spotlight__close')!.click();
    fixture.detectChanges();
    expect(el().querySelector('.player-spotlight')).toBeNull();
    expect(el().querySelector('.board__row--highlighted')).toBeNull();
  });
});
