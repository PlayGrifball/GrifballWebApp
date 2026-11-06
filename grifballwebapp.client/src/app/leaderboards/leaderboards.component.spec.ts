import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LeaderboardsComponent } from './leaderboards.component';
import { By } from '@angular/platform-browser';
import { LeaderboardsDto } from '../api/dtos/leaderboardsDto';
import type { PlayerAllTimeRankDto } from '../api/dtos/playerAllTimeRankDto';
import { PlayerSearchComponent, SEARCH_DEBOUNCE_MS } from './player-search/player-search.component';

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

  it('has no career-total board before the boards load, or on the titles boards', () => {
    expect(fixture.componentInstance.metric()).toBeNull();
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();
    expect(fixture.componentInstance.metric()).toBeNull();
    choose('Dynasties');
    expect(fixture.componentInstance.metric()).toBeNull();
    choose('Kills');
    expect(fixture.componentInstance.metric()!.title).toBe('Kills');
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
      .flush({ ...boards, ringLeaders: [{ rank: 1, userID: 9, gamertag: 'Bracket King', ringsCount: 1, titles: [{ seasonID: 20, seasonName: 'Respawn', tier: 'Open', teamID: 50 }] }] });
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

  const standing = (userID: number, gamertag: string, extra: Partial<PlayerAllTimeRankDto> = {}): PlayerAllTimeRankDto => ({
    userID,
    gamertag,
    matchedAlias: null,
    pastGamertags: [],
    isDummyUser: false,
    rings: { rank: 1, totalRanked: 40, statValue: 5, secondaryText: '5 Titles' },
    goals: { rank: 2, totalRanked: 300, statValue: 120, secondaryText: '4 / G in Halo 3' },
    kills: { rank: 10, totalRanked: 250, statValue: 350, secondaryText: '1.75 K/D' },
    gamesPlayed: { rank: 15, totalRanked: 250, statValue: 30, secondaryText: '3 Seasons' },
    carrierStops: { rank: null, totalRanked: 200, statValue: 0 },
    ...extra,
  });
  const search = () => fixture.debugElement.query(By.directive(PlayerSearchComponent)).componentInstance as PlayerSearchComponent;
  const ranked = (match: { query?: string, userId?: string, format?: string }) => http.expectOne(r => r.url === '/api/Leaderboards/GetPlayerRank'
    && r.params.get('query') === (match.query ?? null) && r.params.get('userId') === (match.userId ?? null) && r.params.get('format') === (match.format ?? null));
  const showBoards = () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();
  };
  const message = () => el().querySelector('.hud-empty')?.textContent!.trim();

  it('shows the standing of a player picked from the suggestions, by their user ID, and highlights their row', () => {
    showBoards();
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null });
    fixture.detectChanges();
    expect(message()).toBe('Searching all-time records...');

    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
    fixture.detectChanges();
    expect(el().querySelector('app-player-spotlight a')!.textContent!.trim()).toBe('Legendary');
    expect(el().querySelector('.hud-empty')).toBeNull();
    expect(el().querySelector('.board__row--highlighted')!.textContent).toContain('Legendary');
    expect(el().querySelector<HTMLInputElement>('#playerSearchInput')!.value).toBe('Legendary');

    // A tile opens its board
    el().querySelectorAll<HTMLButtonElement>('app-player-spotlight .hud-grid button')[1].click();
    fixture.detectChanges();
    expect(fixture.componentInstance.board()).toBe('goals');
    expect(el().querySelector('.hud-tab--active')!.textContent!.trim()).toBe('Goals');
    expect(el().querySelector('.board__row--highlighted')).not.toBeNull();

    // Closing it empties the search and drops the highlight
    el().querySelector<HTMLButtonElement>('[aria-label="Clear player standing"]')!.click();
    fixture.detectChanges();
    expect(el().querySelector('app-player-spotlight')).toBeNull();
    expect(el().querySelector('.board__row--highlighted')).toBeNull();
    expect(el().querySelector<HTMLInputElement>('#playerSearchInput')!.value).toBe('');
  });

  it('looks up the text searched, and says when no player matches', () => {
    showBoards();
    search().query.set('Silva');
    search().submit();
    ranked({ query: 'Silva' }).flush(null);
    fixture.detectChanges();
    expect(message()).toBe('No player found matching "Silva".');
    expect(el().querySelector('app-player-spotlight')).toBeNull();
  });

  it('says when the standing cannot be loaded', () => {
    showBoards();
    search().query.set('Silva');
    search().submit();
    ranked({ query: 'Silva' }).flush('', { status: 500, statusText: 'down' });
    fixture.detectChanges();
    expect(message()).toBe('Failed to load ranking for "Silva".');
    expect(el().querySelector('.hud-empty--error')!.getAttribute('role')).toBe('alert');
  });

  it('cancels an older lookup, so a late answer never replaces the newer player', () => {
    showBoards();
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null });
    const older = ranked({ query: 'Legendary', userId: '1' });
    search().pick({ userID: 2, name: 'Killer', pastGamertag: null });
    expect(older.cancelled).toBeTrue();
    ranked({ query: 'Killer', userId: '2' }).flush(standing(2, 'Killer'));
    fixture.detectChanges();
    expect(fixture.componentInstance.spotlight()!.userID).toBe(2);
  });

  it('clearing the search drops the standing and the lookup in flight', () => {
    showBoards();
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null });
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
    search().pick({ userID: 2, name: 'Killer', pastGamertag: null });
    const pending = ranked({ query: 'Killer', userId: '2' });
    search().clear();
    fixture.detectChanges();
    expect(pending.cancelled).toBeTrue();
    expect(el().querySelector('app-player-spotlight')).toBeNull();
    expect(el().querySelector('.hud-empty')).toBeNull();
  });

  it('looks the standing up again for the format chosen, and not with none shown', () => {
    showBoards();
    const select = el().querySelector('select')!;
    select.value = 'Tournament';
    select.dispatchEvent(new Event('change'));
    http.expectOne(r => r.url === '/api/Leaderboards/GetLeaderboards' && r.params.get('format') === 'Tournament').flush(boards);
    http.expectNone('/api/Leaderboards/GetPlayerRank');

    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null });
    ranked({ query: 'Legendary', userId: '1', format: 'Tournament' }).flush(standing(1, 'Legendary'));
    select.value = '';
    select.dispatchEvent(new Event('change'));
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary', { rings: { rank: 3, totalRanked: 90, statValue: 6 } }));
    fixture.detectChanges();
    expect(el().querySelector('app-player-spotlight .hud-grid button')!.textContent).toContain('#3');
  });

  const chooseFormat = (value: string) => {
    const select = el().querySelector('select')!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
    http.expectOne(r => r.url === '/api/Leaderboards/GetLeaderboards' && r.params.get('format') === (value || null)).flush(boards);
  };

  it('looks up the player last asked for on a new format, not the one still shown', () => {
    showBoards();
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null });
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
    search().pick({ userID: 2, name: 'Killer', pastGamertag: null });
    const pending = ranked({ query: 'Killer', userId: '2' });

    chooseFormat('Tournament');
    expect(pending.cancelled).toBeTrue();
    ranked({ query: 'Killer', userId: '2', format: 'Tournament' }).flush(standing(2, 'Killer'));
    fixture.detectChanges();
    expect(fixture.componentInstance.spotlight()!.userID).toBe(2);
  });

  it('looks the first player up again for a new format chosen while their standing loads', () => {
    showBoards();
    search().query.set('Silva');
    search().submit();
    const pending = ranked({ query: 'Silva' });

    chooseFormat('Tournament');
    expect(pending.cancelled).toBeTrue();
    ranked({ query: 'Silva', format: 'Tournament' }).flush(standing(5, 'Silva'));
    fixture.detectChanges();
    expect(fixture.componentInstance.spotlight()!.userID).toBe(5);
  });

  it('looks nobody up on a new format once the standing is closed', () => {
    showBoards();
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null });
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
    fixture.componentInstance.clearSpotlight();
    chooseFormat('Tournament');
    http.expectNone(r => r.url === '/api/Leaderboards/GetPlayerRank');
  });

  it('closing the standing drops the suggestions the box was still searching for', () => {
    jasmine.clock().install();
    try {
      showBoards();
      search().pick({ userID: 1, name: 'Legendary', pastGamertag: null });
      ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
      fixture.detectChanges();
      const input = el().querySelector<HTMLInputElement>('#playerSearchInput')!;
      input.value = 'ab';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      el().querySelector<HTMLButtonElement>('[aria-label="Clear player standing"]')!.click();
      fixture.detectChanges();
      jasmine.clock().tick(SEARCH_DEBOUNCE_MS * 2);
      http.expectNone(r => r.url === '/api/Leaderboards/SearchPlayers');
      expect(input.value).toBe('');
      expect(search().open()).toBeFalse();
      expect(el().querySelector('app-player-spotlight')).toBeNull();
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
