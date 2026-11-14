import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { HttpRequest } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { LeaderboardsComponent } from './leaderboards.component';
import { By } from '@angular/platform-browser';
import type { LeaderboardsDto } from '@api/models';
import type { LeaderboardFilter, PlayerAllTimeRankDto } from '@api/models';
import { PlayerSearchComponent } from './playerSearch/playerSearch.component';
import { SEARCH_DEBOUNCE_MS } from './playerSearch/playerSearch.store';

describe('LeaderboardsComponent', () => {
  let fixture: ComponentFixture<LeaderboardsComponent>;
  let http: HttpTestingController;
  const leader = (userID: number, gamertag: string, statValue: number, perGame: number | null = 1.5) =>
    ({ rank: 1, userID, gamertag, statValue, gamesPlayed: 30, gamesDecidedByVote: 0, perGame, seasonsCount: 3, gameVersionName: null, secondaryStat: null });
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
  /** The formats a request asks for (null: none named, every format). */
  const asks = (formats: string[] | null) => (r: HttpRequest<unknown>) => JSON.stringify(r.params.getAll('formats')) === JSON.stringify(formats);
  const expectBoards = (formats: string[] | null = null) => http.expectOne(r => r.url === '/api/leaderboards' && asks(formats)(r));
  /** Opens the format select, its options in the overlay. */
  const openFormats = () => {
    el().querySelector<HTMLElement>('mat-select .mat-mdc-select-trigger')!.click();
    fixture.detectChanges();
  };
  const option = (label: string) => Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find(o => o.textContent!.trim() === label)!;
  const toggleFormat = (label: string) => {
    option(label).click();
    fixture.detectChanges();
  };
  const formatsText = () => el().querySelector('mat-select .mat-mdc-select-value')!.textContent!.trim();

  it('shows each board, linking players to their profiles and dynasties to their franchise history', () => {
    expectBoards().flush(boards);
    fixture.detectChanges();

    expect(rows()).toEqual([['1', 'Legendary', '5', 'Summer League 2011 (Pro), Winter League 2012 (Pro)']]);
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/profile/1');
    expect([...el().querySelectorAll('.board__titles a')].map(a => a.getAttribute('href'))).toEqual(['/season/11', '/season/12']);

    choose('Dynasties');
    expect(rows()[0].slice(0, 4)).toEqual(['1', 'Relentless', '4', '6']);
    expect(el().querySelector('.board__titles a')!.getAttribute('href')).toBe('/season/11/team/40');
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/history/team/40');

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
    expectBoards().flush(boards);
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
    expectBoards().flush(boards);
    fixture.detectChanges();
    expect(fixture.componentInstance.metric()).toBeNull();
    choose('Dynasties');
    expect(fixture.componentInstance.metric()).toBeNull();
    choose('Kills');
    expect(fixture.componentInstance.metric()!.title).toBe('Kills');
  });

  it('offers the formats on every board, all of them chosen to start, and counts any of them chosen', async () => {
    expectBoards().flush(boards);
    fixture.detectChanges();
    // The select takes its first value once its options are there
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el().querySelector('mat-form-field mat-label')!.textContent!.trim()).toBe('Format');
    expect(formatsText()).toBe('All formats');
    choose('Dynasties');
    expect(el().querySelector('mat-select')).withContext('Titles follow the formats too').not.toBeNull();

    openFormats();
    expect(Array.from(document.querySelectorAll('mat-option')).map(o => o.textContent!.trim())).toEqual(['League season', 'Tournament', 'Late League']);
    expect(Array.from(document.querySelectorAll('mat-option')).map(o => o.getAttribute('aria-selected'))).toEqual(['true', 'true', 'true']);

    toggleFormat('League season');
    expectBoards(['Tournament', 'LateLeague']).flush(boards);
    expect(formatsText()).toBe('Tournament, Late League');
    toggleFormat('Late League');
    expectBoards(['Tournament']).flush({ ...boards, goalLeaders: [leader(9, 'Bracket King', 42)] });
    choose('Goals');
    expect(rows().map(r => r[1])).toEqual(['Bracket King']);
    expect(formatsText()).toBe('Tournament');
  });

  it('counts every format again once none is chosen, and shows the boards last asked for', async () => {
    expectBoards().flush(boards);
    fixture.componentInstance.setFormats(['Tournament']);
    const older = expectBoards(['Tournament']);
    fixture.componentInstance.setFormats([]);
    expect(older.cancelled).withContext('the older boards never replace the newer').toBeTrue();
    expectBoards().flush(boards);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.componentInstance.formats()).toEqual(['LeagueSeason', 'Tournament', 'LateLeague']);
    expect(formatsText()).toBe('All formats');
  });

  it('keeps the formats in the select\'s order, whatever order they were chosen in', () => {
    expectBoards().flush(boards);
    fixture.componentInstance.setFormats(['LateLeague', 'LeagueSeason']);
    expectBoards(['LeagueSeason', 'LateLeague']).flush(boards);
    fixture.detectChanges();
    expect(formatsText()).toBe('League season, Late League');
  });

  it('says how many of the games played were decided by vote next to a figure per game, which leaves them out', () => {
    expectBoards().flush({
      ...boards,
      killLeaders: [{ ...leader(2, 'Killer', 500), gamesDecidedByVote: 1 }, leader(5, 'Boxed', 400)],
      gamesPlayedLeaders: [{ ...leader(3, 'Ironman', 30, null), gamesDecidedByVote: 1 }],
    });
    fixture.detectChanges();
    choose('Kills');
    expect(rows().map(r => r[3])).toEqual(['30 (1 by vote)', '30']);
    choose('Games played');
    expect(rows()[0][3]).withContext('No figure per game on this board').toBe('30');
  });

  it('says when the leaderboards cannot be loaded', () => {
    expectBoards().flush('', { status: 500, statusText: 'down' });
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
    carrierStops: { rank: null, totalRanked: 200, statValue: 0, secondaryText: null },
    ...extra,
  });
  const search = () => fixture.debugElement.query(By.directive(PlayerSearchComponent)).componentInstance as PlayerSearchComponent;
  const ranked = (match: { query?: string, userId?: string, formats?: string[] }) => http.expectOne(r => r.url === '/api/leaderboards/player-rank'
    && r.params.get('query') === (match.query ?? null) && r.params.get('userID') === (match.userId ?? null) && asks(match.formats ?? null)(r));
  const showBoards = () => {
    expectBoards().flush(boards);
    fixture.detectChanges();
  };
  const message = () => el().querySelector('.hud-empty')?.textContent!.trim();

  it('shows the standing of a player picked from the suggestions, by their user ID, and highlights their row', () => {
    showBoards();
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null, matchedName: null, kind: null, years: null });
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
    ranked({ query: 'Silva' }).flush({ status: 404, title: 'Not found' }, { status: 404, statusText: 'Not Found' });
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
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null, matchedName: null, kind: null, years: null });
    const older = ranked({ query: 'Legendary', userId: '1' });
    search().pick({ userID: 2, name: 'Killer', pastGamertag: null, matchedName: null, kind: null, years: null });
    expect(older.cancelled).toBeTrue();
    ranked({ query: 'Killer', userId: '2' }).flush(standing(2, 'Killer'));
    fixture.detectChanges();
    expect(fixture.componentInstance.spotlight()!.userID).toBe(2);
  });

  it('clearing the search drops the standing and the lookup in flight', () => {
    showBoards();
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null, matchedName: null, kind: null, years: null });
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
    search().pick({ userID: 2, name: 'Killer', pastGamertag: null, matchedName: null, kind: null, years: null });
    const pending = ranked({ query: 'Killer', userId: '2' });
    search().clear();
    fixture.detectChanges();
    expect(pending.cancelled).toBeTrue();
    expect(el().querySelector('app-player-spotlight')).toBeNull();
    expect(el().querySelector('.hud-empty')).toBeNull();
  });

  it('looks the standing up again for the formats chosen, and not with none shown', () => {
    showBoards();
    chooseFormats('Tournament');
    http.expectNone('/api/leaderboards/player-rank');

    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null, matchedName: null, kind: null, years: null });
    ranked({ query: 'Legendary', userId: '1', formats: ['Tournament'] }).flush(standing(1, 'Legendary'));
    chooseFormats('LeagueSeason', 'Tournament', 'LateLeague');
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary', { rings: { rank: 3, totalRanked: 90, statValue: 6, secondaryText: null } }));
    fixture.detectChanges();
    expect(el().querySelector('app-player-spotlight .hud-grid button')!.textContent).toContain('#3');
  });

  /** Chooses these formats, and answers the boards asked for. */
  const chooseFormats = (...formats: LeaderboardFilter[]) => {
    fixture.componentInstance.setFormats(formats);
    expectBoards(formats.length === 3 ? null : formats).flush(boards);
  };

  it('looks up the player last asked for on a new format, not the one still shown', () => {
    showBoards();
    choose('Goals');
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null, matchedName: null, kind: null, years: null });
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
    search().pick({ userID: 2, name: 'Killer', pastGamertag: null, matchedName: null, kind: null, years: null });
    const pending = ranked({ query: 'Killer', userId: '2' });

    chooseFormats('Tournament');
    expect(pending.cancelled).toBeTrue();
    ranked({ query: 'Killer', userId: '2', formats: ['Tournament'] }).flush(standing(2, 'Killer'));
    fixture.detectChanges();
    expect(fixture.componentInstance.spotlight()!.userID).toBe(2);
  });

  it('looks the first player up again for a new format chosen while their standing loads', () => {
    showBoards();
    choose('Goals');
    search().query.set('Silva');
    search().submit();
    const pending = ranked({ query: 'Silva' });

    chooseFormats('Tournament');
    expect(pending.cancelled).toBeTrue();
    ranked({ query: 'Silva', formats: ['Tournament'] }).flush(standing(5, 'Silva'));
    fixture.detectChanges();
    expect(fixture.componentInstance.spotlight()!.userID).toBe(5);
  });

  it('looks nobody up on a new format once the standing is closed', () => {
    showBoards();
    choose('Goals');
    search().pick({ userID: 1, name: 'Legendary', pastGamertag: null, matchedName: null, kind: null, years: null });
    ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
    fixture.componentInstance.clearSpotlight();
    chooseFormats('Tournament');
    expect(http.match(r => r.url === '/api/leaderboards/player-rank')).withContext('rank lookups').toEqual([]);
  });

  it('closing the standing drops the suggestions the box was still searching for', () => {
    jasmine.clock().install();
    try {
      showBoards();
      search().pick({ userID: 1, name: 'Legendary', pastGamertag: null, matchedName: null, kind: null, years: null });
      ranked({ query: 'Legendary', userId: '1' }).flush(standing(1, 'Legendary'));
      fixture.detectChanges();
      const input = el().querySelector<HTMLInputElement>('#playerSearchInput')!;
      input.value = 'ab';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      el().querySelector<HTMLButtonElement>('[aria-label="Clear player standing"]')!.click();
      fixture.detectChanges();
      jasmine.clock().tick(SEARCH_DEBOUNCE_MS * 2);
      http.expectNone(r => r.url === '/api/leaderboards/players');
      expect(input.value).toBe('');
      expect(search().open()).toBeFalse();
      expect(el().querySelector('app-player-spotlight')).toBeNull();
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
