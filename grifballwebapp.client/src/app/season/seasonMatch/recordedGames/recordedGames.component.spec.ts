import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { DateTime } from 'luxon';
import { INFINITE_GAME_VERSION_ID, ManualGameForm, RecordedGamesComponent, toLocalInput, toRequest } from './recordedGames.component';
import { RecordedSeriesGameDto, SeasonMatchPageDto } from '../seasonMatchPageDto';
import { PlayerDto } from '../../team/team.component';

describe('RecordedGamesComponent', () => {
  let fixture: ComponentFixture<RecordedGamesComponent>;
  let component: RecordedGamesComponent;
  let httpMock: HttpTestingController;
  let changed: number;

  const page = (overrides: Partial<SeasonMatchPageDto> = {}): SeasonMatchPageDto => ({
    seasonID: 3, seasonName: 'Season 3', isPlayoff: false,
    homeTeamName: 'Reds', homeTeamID: 10, homeTeamScore: null, homeTeamResult: 'TBD',
    awayTeamName: 'Blues', awayTeamID: 20, awayTeamScore: null, awayTeamResult: 'TBD',
    scheduledTime: null, bestOf: 3, reportedGames: [], recordedGames: [], isImported: false,
    bracketInfo: null, activeRescheduleRequestId: null, canRequestReschedule: false,
    ...overrides
  });

  const game = (overrides: Partial<RecordedSeriesGameDto> = {}): RecordedSeriesGameDto => ({
    recordedGameID: 41, gameNumber: 1, gameVersionID: 1, gameVersionName: 'Halo 3', map: 'Foundry', gametype: 'Grifball',
    playedAt: null, homeTeamScore: 5, awayTeamScore: 2, homeTeamWon: true, awayTeamWon: false, isManual: true,
    players: [
      { userID: 1, gamertag: 'Grif', isHomeTeam: true, kills: 9, deaths: 3, assists: 1, score: 2 },
      { userID: 99, gamertag: 'Sub', isHomeTeam: false, kills: 1, deaths: 6, assists: 0, score: null },
    ],
    ...overrides
  });

  const roster = (userID: number, gamertag: string | null): PlayerDto =>
    ({ teamPlayerID: userID * 10, userID, gamertag, draftCaptainOrder: null, draftRound: null });

  function create(match: SeasonMatchPageDto, canEdit: boolean): void {
    fixture = TestBed.createComponent(RecordedGamesComponent);
    component = fixture.componentInstance;
    changed = 0;
    component.changed.subscribe(() => changed++);
    fixture.componentRef.setInput('seasonMatchID', 7);
    fixture.componentRef.setInput('match', match);
    fixture.componentRef.setInput('homeRoster', [roster(1, 'Grif'), roster(2, null)]);
    fixture.componentRef.setInput('awayRoster', [roster(3, 'Tucker')]);
    fixture.componentRef.setInput('canEdit', canEdit);
    fixture.detectChanges();
  }

  /** As a commissioner: the game versions are asked for first. */
  function createAsCommissioner(match: SeasonMatchPageDto = page()): void {
    create(match, true);
    httpMock.expectOne('/api/EventOrganizer/GetGameVersions')
      .flush([{ gameVersionID: 1, name: 'Halo 3' }, { gameVersionID: INFINITE_GAME_VERSION_ID, name: 'Halo Infinite' }]);
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
    .find(b => b.textContent!.trim() === label) as HTMLButtonElement | undefined;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecordedGamesComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideAnimations()],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists the recorded games with their score, game and players, for everyone, without the tools', () => {
    create(page({ recordedGames: [game()] }), false);

    expect(text()).toContain('Game 1');
    expect(text()).toContain('Halo 3 · Foundry · Grifball');
    expect(text()).toContain('Entered by hand');
    const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr'))
      .map(tr => Array.from(tr.querySelectorAll('td')).map(td => td.textContent!.trim()));
    expect(rows).toEqual([['Grif', 'Reds', '9', '3', '1', '2'], ['Sub', 'Blues', '1', '6', '0', '-']]);
    expect(button('Edit')).toBeUndefined();
    expect(button('Delete')).toBeUndefined();
    expect(button('Enter a game by hand')).toBeUndefined();
  });

  it('takes a page without recorded games listed, and does nothing with nothing to save or delete', () => {
    const match = page();
    delete match.recordedGames;
    createAsCommissioner(match);
    expect(component.games()).toEqual([]);

    component.save();
    component.confirmDelete();
    httpMock.expectNone(() => true);
  });

  it('copes with no game version list from the server', () => {
    create(page(), true);
    httpMock.expectOne('/api/EventOrganizer/GetGameVersions').flush(null);
    expect(component.gameVersions()).toEqual([]);
  });

  it('edits a game entered without a map or gametype', () => {
    createAsCommissioner(page({ recordedGames: [game({ map: null, gametype: null, players: [] })] }));
    component.startEdit(component.games()[0]);
    expect([component.form.map, component.form.gametype, component.form.playedAt]).toEqual(['', '', '']);
  });

  it('shows nothing for a series without recorded games', () => {
    create(page(), false);
    expect(text().trim()).toBe('');
  });

  it('enters a game: the ticked players only, then tells the page', () => {
    createAsCommissioner();

    button('Enter a game by hand')!.click();
    fixture.detectChanges();
    expect(component.form.gameVersionID).toBe(INFINITE_GAME_VERSION_ID);
    expect(component.form.players.map(p => [p.userID, p.gamertag, p.isHomeTeam, p.played]))
      .toEqual([[1, 'Grif', true, false], [2, 'Unknown Spartan', true, false], [3, 'Tucker', false, false]]);
    expect(button('Save game')!.disabled).toBeTrue();

    component.form.homeTeamScore = 3;
    component.form.awayTeamScore = 5;
    component.form.map = ' Pit ';
    Object.assign(component.form.players[2], { played: true, kills: 8, deaths: 2, assists: 1, score: 4 });
    component.save();

    const request = httpMock.expectOne('/api/SeasonMatch/AddManualGame/7');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      gameVersionID: INFINITE_GAME_VERSION_ID, map: 'Pit', gametype: null, playedAt: null, homeTeamScore: 3, awayTeamScore: 5,
      players: [{ userID: 3, isHomeTeam: false, kills: 8, deaths: 2, assists: 1, score: 4 }],
    });
    request.flush(12);
    fixture.detectChanges();
    expect(changed).toBe(1);
    expect(component.editing()).toBeNull();
  });

  it('will not save equal scores, and says why', () => {
    createAsCommissioner();
    component.startAdd();
    component.form.homeTeamScore = 4;
    component.form.awayTeamScore = 4;
    fixture.detectChanges();

    expect(component.formValid()).toBeFalse();
    expect(text()).toContain("the two scores can't be equal");
    component.save();
    httpMock.expectNone('/api/SeasonMatch/AddManualGame/7');
  });

  it('cancels an entry', () => {
    createAsCommissioner();
    component.startAdd();
    fixture.detectChanges();
    button('Cancel')!.click();
    fixture.detectChanges();
    expect(component.editing()).toBeNull();
    expect(button('Enter a game by hand')).toBeDefined();
  });

  it('edits a game entered by hand, filled in from it (its subs too)', () => {
    createAsCommissioner(page({ recordedGames: [game({ playedAt: '2026-01-10T20:30:00Z' })] }));

    button('Edit')!.click();
    fixture.detectChanges();
    expect(component.editing()).toBe(41);
    expect(component.form.playedAt).toBe(toLocalInput('2026-01-10T20:30:00Z'));
    expect(component.form.players.map(p => [p.userID, p.played, p.kills])).toEqual([[1, true, 9], [2, false, 0], [3, false, 0], [99, true, 1]]);

    component.form.homeTeamScore = 1;
    component.save();
    const request = httpMock.expectOne('/api/SeasonMatch/EditManualGame/7/41');
    expect(request.request.body.homeTeamScore).toBe(1);
    expect(request.request.body.playedAt).toBe('2026-01-10T20:30:00.000Z');
    expect(request.request.body.players.map((p: { userID: number }) => p.userID)).toEqual([1, 99]);
    request.flush(null);
    expect(changed).toBe(1);
  });

  it('deletes a game once confirmed, and shows why the server refused', () => {
    createAsCommissioner(page({ recordedGames: [game()] }));

    button('Delete')!.click();
    fixture.detectChanges();
    expect(text()).toContain('Delete game 1?');
    button('Cancel')!.click();
    fixture.detectChanges();
    expect(component.pendingDelete()).toBeNull();

    button('Delete')!.click();
    fixture.detectChanges();
    button('Confirm')!.click();
    httpMock.expectOne('/api/SeasonMatch/DeleteManualGame/7/41').flush('Season match 9 that this result fed into already has a result of its own. Undo that match first.', { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
    expect(text()).toContain('already has a result of its own');
    expect(changed).toBe(0);

    button('Confirm')!.click();
    httpMock.expectOne('/api/SeasonMatch/DeleteManualGame/7/41').flush(null);
    expect(changed).toBe(1);
    expect(component.pendingDelete()).toBeNull();
  });

  it('falls back to a message of its own when the server gives none', () => {
    createAsCommissioner();
    component.startAdd();
    component.form.homeTeamScore = 5;
    component.form.awayTeamScore = 0;
    component.save();
    httpMock.expectOne('/api/SeasonMatch/AddManualGame/7').flush(null, { status: 500, statusText: 'Server Error' });
    expect(component.error()).toBe('The server could not save that.');
  });

  it('offers no entry for a decided match, a full series, an imported season or a match missing a team', () => {
    for (const match of [
      page({ homeTeamResult: 'Won', awayTeamResult: 'Loss' }),
      page({ bestOf: 1, reportedGames: [{ matchID: 'g-1', matchNumber: 1 }] }),
      page({ isImported: true }),
      page({ awayTeamID: null }),
    ]) {
      create(match, true);
      httpMock.match('/api/EventOrganizer/GetGameVersions').forEach(r => r.flush([]));
      fixture.detectChanges();
      expect(component.canAdd()).withContext(JSON.stringify(match)).toBeFalse();
    }
  });

  it('lets only games entered by hand be changed, and not once a forfeit decided the match', () => {
    createAsCommissioner(page({ recordedGames: [game({ isManual: false })] }));
    expect(button('Edit')).toBeUndefined();

    fixture.componentRef.setInput('match', page({ recordedGames: [game()], homeTeamResult: 'Forfeit', awayTeamResult: 'Won' }));
    fixture.detectChanges();
    expect(button('Edit')).toBeUndefined();
    expect(component.canChange(game())).toBeFalse();
  });

  it('defaults to the first game listed when Halo Infinite is not among them, and copes with no list', () => {
    create(page(), true);
    httpMock.expectOne('/api/EventOrganizer/GetGameVersions').flush([{ gameVersionID: 2, name: 'Halo Reach' }]);
    component.startAdd();
    expect(component.form.gameVersionID).toBe(2);

    create(page(), true);
    httpMock.expectOne('/api/EventOrganizer/GetGameVersions').flush(null, { status: 500, statusText: 'Server Error' });
    component.startAdd();
    expect(component.form.gameVersionID).toBe(INFINITE_GAME_VERSION_ID);
  });
});

describe('recorded game helpers', () => {
  const form = (overrides: Partial<ManualGameForm> = {}): ManualGameForm => ({
    gameVersionID: 1, map: '', gametype: ' Grifball ', playedAt: '', homeTeamScore: 5, awayTeamScore: 3, players: [], ...overrides,
  });

  it('turns a local time into UTC, and leaves out a bad one', () => {
    const local = '2026-01-10T20:30';
    expect(toRequest(form({ playedAt: local })).playedAt).toBe(DateTime.fromISO(local).toUTC().toISO());
    expect(toRequest(form({ playedAt: 'not a time' })).playedAt).toBeNull();
    expect(toRequest(form()).gametype).toBe('Grifball');
  });

  it('fills blank stats with zero and a blank score with none', () => {
    const line = { userID: 1, gamertag: 'Grif', isHomeTeam: true, played: true, kills: null as unknown as number, deaths: 2, assists: null as unknown as number, score: null };
    expect(toRequest(form({ homeTeamScore: null, awayTeamScore: null, players: [line] }))).toEqual(jasmine.objectContaining({
      homeTeamScore: 0,
      awayTeamScore: 0,
      players: [{ userID: 1, isHomeTeam: true, kills: 0, deaths: 2, assists: 0, score: null }],
    }));
  });

  it('shows no time for none or a bad one', () => {
    expect(toLocalInput(null)).toBe('');
    expect(toLocalInput('garbage')).toBe('');
  });
});
