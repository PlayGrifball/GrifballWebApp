import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { DateTime } from 'luxon';
import { INFINITE_GAME_VERSION_ID, ManualGameForm, ManualGamesComponent, toLocalInput, toRequest } from './manualGames.component';
import { ReportedGameDto, SeasonMatchPageDto } from '../seasonMatchPageDto';
import { PlayerDto } from '../../team/team.component';

describe('ManualGamesComponent', () => {
  let fixture: ComponentFixture<ManualGamesComponent>;
  let component: ManualGamesComponent;
  let httpMock: HttpTestingController;
  let changed: number;

  const page = (overrides: Partial<SeasonMatchPageDto> = {}): SeasonMatchPageDto => ({
    seasonID: 3, seasonName: 'Season 3', isPlayoff: false,
    homeTeamName: 'Reds', homeTeamID: 10, homeTeamScore: null, homeTeamResult: 'TBD',
    awayTeamName: 'Blues', awayTeamID: 20, awayTeamScore: null, awayTeamResult: 'TBD',
    scheduledTime: null, bestOf: 3, reportedGames: [], isImported: false,
    decidedBy: null, canUndo: false,
    bracketInfo: null, activeRescheduleRequestId: null, canRequestReschedule: false,
    ...overrides
  });
  /** Decided by its games, and by a forfeit, as the server sends it. */
  const wonByGames: Partial<SeasonMatchPageDto> = { homeTeamResult: 'Won', awayTeamResult: 'Loss', decidedBy: 'Games' };
  const forfeited: Partial<SeasonMatchPageDto> = { homeTeamResult: 'Forfeit', awayTeamResult: 'Won', decidedBy: 'Forfeit' };

  /** A game entered by hand, as the box score has it (home and away players, goals as bombScores). */
  const game = (overrides: Partial<ReportedGameDto> = {}): ReportedGameDto => ({
    seriesGameID: 51, matchID: null, recordedGameID: 41, matchNumber: 1, homeScore: 5, awayScore: 2,
    isManual: true, gameVersionID: 1, gameVersionName: 'Halo 3', map: 'Foundry', gametype: 'Grifball', playedAt: null,
    homePlayers: [{ userID: 1, gamertag: 'Grif', kills: 9, deaths: 3, assists: 1, bombScores: 2, score: 2 }],
    awayPlayers: [
      { userID: 99, gamertag: 'Sub', kills: 1, deaths: 6, assists: 0, bombScores: 0, score: null },
      // A player without a user can't be on a game entered by hand: left out of the form
      { userID: null, gamertag: 'Ghost', kills: 0, deaths: 0, assists: 0, bombScores: 0, score: null },
    ],
    ...overrides
  });

  const roster = (userID: number, gamertag: string | null): PlayerDto =>
    ({ teamPlayerID: userID * 10, userID, gamertag, draftCaptainOrder: null, draftRound: null });

  function create(match: SeasonMatchPageDto | null, canEdit: boolean): void {
    fixture = TestBed.createComponent(ManualGamesComponent);
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
    httpMock.expectOne('/api/game-versions')
      .flush([{ gameVersionID: 1, name: 'Halo 3' }, { gameVersionID: INFINITE_GAME_VERSION_ID, name: 'Halo Infinite' }]);
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
    .find(b => b.textContent!.trim() === label) as HTMLButtonElement | undefined;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManualGamesComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideAnimations()],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('shows nothing, and asks for nothing, for a visitor or while the page loads', () => {
    create(page({ reportedGames: [game()] }), false);
    expect(text().trim()).toBe('');
    expect(component.canChange(game())).toBeFalse();

    create(null, true);
    expect(text().trim()).toBe('');
    expect(component.canAdd()).toBeFalse();
  });

  it('does nothing with nothing to save or delete', () => {
    createAsCommissioner();
    component.save();
    component.confirmDelete();
    expect(httpMock.match(() => true)).withContext('requests sent').toEqual([]);
  });

  it('copes with no game version list from the server', () => {
    create(page(), true);
    httpMock.expectOne('/api/game-versions').flush(null);
    expect(component.gameVersions()).toEqual([]);
  });

  it('counts every game of the series once: the deciding game of a best of 3 at 1-1 can still be entered', () => {
    // Two games entered by hand, 1-1: the box score has both (Waypoint and recorded games alike)
    createAsCommissioner(page({ reportedGames: [game(), game({ seriesGameID: 52, recordedGameID: 42, matchNumber: 2, homeScore: 1, awayScore: 5 })] }));
    expect(component.canAdd()).toBeTrue();
    expect(button('Enter a game by hand')).toBeDefined();

    // A full series takes no new game: only one replacing a game of it
    fixture.componentRef.setInput('match', page({ bestOf: 2, reportedGames: [game(), game({ seriesGameID: 52, recordedGameID: 42, matchNumber: 2 })] }));
    fixture.detectChanges();
    expect(component.slots().map(choice => choice.slot)).toEqual([51, 52]);
  });

  it('enters a game: the ticked players only, with their goals, then tells the page', () => {
    createAsCommissioner();

    button('Enter a game by hand')!.click();
    fixture.detectChanges();
    expect(component.form.gameVersionID).toBe(INFINITE_GAME_VERSION_ID);
    expect(component.form.players.map(p => [p.userID, p.gamertag, p.isHomeTeam, p.played, p.goals]))
      .toEqual([[1, 'Grif', true, false, 0], [2, 'Unknown Spartan', true, false, 0], [3, 'Tucker', false, false, 0]]);
    expect(button('Save game')!.disabled).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).querySelector('input[aria-label="Tucker goals"]')).not.toBeNull();

    component.form.homeTeamScore = 3;
    component.form.awayTeamScore = 5;
    component.form.map = ' Pit ';
    Object.assign(component.form.players[2], { played: true, kills: 8, deaths: 2, assists: 1, goals: 3, score: 4 });
    component.save();

    const request = httpMock.expectOne('/api/SeasonMatch/AddManualGame/7');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      gameVersionID: INFINITE_GAME_VERSION_ID, map: 'Pit', gametype: null, playedAt: null, homeTeamScore: 3, awayTeamScore: 5,
      players: [{ userID: 3, isHomeTeam: false, kills: 8, deaths: 2, assists: 1, goals: 3, score: 4 }],
      replacesSeriesGameID: null,
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
    expect(component.idle()).toBeFalse();
    fixture.detectChanges();
    button('Cancel')!.click();
    fixture.detectChanges();
    expect(component.editing()).toBeNull();
    expect(component.idle()).toBeTrue();
    expect(button('Enter a game by hand')).toBeDefined();
  });

  it('edits a game from the box score, filled in from it (its subs and goals too)', () => {
    createAsCommissioner(page({ reportedGames: [game({ playedAt: '2026-01-10T20:30:00Z' })] }));

    component.startEdit(game({ playedAt: '2026-01-10T20:30:00Z' }));
    fixture.detectChanges();
    expect(component.editing()).toBe(41);
    expect(text()).toContain('Edit game');
    expect([component.form.gameVersionID, component.form.map, component.form.gametype, component.form.homeTeamScore, component.form.awayTeamScore])
      .toEqual([1, 'Foundry', 'Grifball', 5, 2]);
    expect(component.form.playedAt).toBe(toLocalInput('2026-01-10T20:30:00Z'));
    expect(component.form.players.map(p => [p.userID, p.isHomeTeam, p.played, p.kills, p.goals]))
      .toEqual([[1, true, true, 9, 2], [2, true, false, 0, 0], [3, false, false, 0, 0], [99, false, true, 1, 0]]);

    component.form.homeTeamScore = 1;
    component.save();
    const request = httpMock.expectOne('/api/SeasonMatch/EditManualGame/7/41');
    expect(request.request.body.homeTeamScore).toBe(1);
    expect(request.request.body.playedAt).toBe('2026-01-10T20:30:00.000Z');
    expect(request.request.body.players.map((p: { userID: number, goals: number }) => [p.userID, p.goals])).toEqual([[1, 2], [99, 0]]);
    request.flush(null);
    expect(changed).toBe(1);
  });

  it('edits a game entered without a map, gametype, time, players or game version', () => {
    createAsCommissioner(page({ reportedGames: [game()] }));
    component.startEdit(game({ map: null, gametype: null, gameVersionID: null, homeScore: null, awayScore: null, homePlayers: undefined, awayPlayers: undefined }));
    expect([component.form.map, component.form.gametype, component.form.playedAt]).toEqual(['', '', '']);
    expect(component.form.gameVersionID).toBe(INFINITE_GAME_VERSION_ID);
    expect([component.form.homeTeamScore, component.form.awayTeamScore]).toEqual([null, null]);
    expect(component.form.players.every(p => !p.played)).toBeTrue();
  });

  it('asks whether a game entered by hand is new or replaces one, picking neither, and sends the game it replaces', () => {
    createAsCommissioner(page({ reportedGames: [game({ seriesGameID: 60, matchID: 'g-1', recordedGameID: null, isManual: false })] }));
    component.startAdd();
    component.form.homeTeamScore = 5;
    component.form.awayTeamScore = 1;
    fixture.detectChanges();
    expect(component.form.slot).toBeNull();
    expect(component.formValid()).toBeFalse();
    expect(component.slots().map(choice => choice.label)).toEqual(['A new game (game 2)', 'Replaces game 1 (from Waypoint)']);

    component.form.slot = 60;
    expect(component.formValid()).toBeTrue();
    component.save();
    expect(httpMock.expectOne('/api/SeasonMatch/AddManualGame/7').request.body.replacesSeriesGameID).toBe(60);
  });

  it('a decided series takes no new game, only a replacement; a forfeit takes neither', () => {
    createAsCommissioner(page({ ...wonByGames, reportedGames: [game()] }));
    expect(component.slots().map(choice => choice.slot)).toEqual([51]);
    fixture.componentRef.setInput('match', page({ ...forfeited, reportedGames: [game()] }));
    expect(component.slots()).toEqual([]);
    expect(component.canAdd()).toBeFalse();
  });

  it('unreports a game once confirmed, says what becomes of it, and shows why the server refused', () => {
    createAsCommissioner(page({ reportedGames: [game()] }));

    component.requestDelete(game());
    fixture.detectChanges();
    expect(text()).toContain('Unreport game 1?');
    expect(text()).toContain('deleted for good');
    expect(button('Enter a game by hand')).toBeUndefined();
    button('Cancel')!.click();
    fixture.detectChanges();
    expect(component.pendingDelete()).toBeNull();

    component.requestDelete(game());
    fixture.detectChanges();
    button('Confirm')!.click();
    httpMock.expectOne('/api/SeasonMatch/RemoveGame/7/51').flush('Season match 9 that this result fed into already has games. Undo that match first.', { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
    expect(text()).toContain('already has games');
    expect(changed).toBe(0);

    button('Confirm')!.click();
    httpMock.expectOne('/api/SeasonMatch/RemoveGame/7/51').flush(null);
    expect(changed).toBe(1);
    expect(component.pendingDelete()).toBeNull();
  });

  it('starting an edit drops a delete waiting, and the other way round', () => {
    createAsCommissioner(page({ reportedGames: [game()] }));
    component.requestDelete(game());
    component.startEdit(game());
    expect(component.pendingDelete()).toBeNull();
    component.requestDelete(game());
    expect(component.editing()).toBeNull();
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

  it('offers no entry for a match decided without games, or by a forfeit, an imported season or a match missing a team', () => {
    for (const match of [
      page({ ...wonByGames }),
      page({ ...forfeited, reportedGames: [{ seriesGameID: 60, matchID: 'g-1', matchNumber: 1 }] }),
      page({ isImported: true }),
      page({ awayTeamID: null }),
    ]) {
      create(match, true);
      httpMock.match('/api/game-versions').forEach(r => r.flush([]));
      fixture.detectChanges();
      expect(component.canAdd()).withContext(JSON.stringify(match)).toBeFalse();
    }
  });

  it('lets only games entered by hand be edited, any game be unreported, and neither once a forfeit decided the match', () => {
    createAsCommissioner(page({ reportedGames: [game({ isManual: false })] }));
    expect(component.canChange(game({ isManual: false }))).toBeFalse();
    expect(component.canChange(game({ recordedGameID: null }))).toBeFalse();
    expect(component.canChange(null)).toBeFalse();
    component.startEdit(game({ isManual: false }));
    expect(component.editing()).toBeNull();
    component.requestDelete(game({ isManual: false }));
    fixture.detectChanges();
    expect(component.pendingDelete()).toEqual(game({ isManual: false }));
    expect(text()).toContain('Unreport game 1?');
    component.cancel();

    fixture.componentRef.setInput('match', page({ reportedGames: [game()], ...forfeited }));
    fixture.detectChanges();
    expect(component.canChange(game())).toBeFalse();
    expect(component.canRemove(game())).toBeFalse();
    // Nor from the box score's buttons, were they still there
    component.requestDelete(game());
    component.startEdit(game());
    expect(component.pendingDelete()).toBeNull();
    expect(component.editing()).toBeNull();
  });

  it('is idle, then a game in the form or an unreport waiting, one at a time', () => {
    createAsCommissioner(page({ reportedGames: [game()] }));
    expect(component.mode()).toEqual({ kind: 'idle' });

    component.startAdd();
    expect(component.mode()).toEqual({ kind: 'entering', target: 'new' });
    component.startEdit(game());
    expect(component.mode()).toEqual({ kind: 'entering', target: 41 });
    component.requestDelete(game());
    expect(component.mode()).toEqual({ kind: 'confirmDelete', game: game() });
    component.cancel();
    expect(component.mode()).toEqual({ kind: 'idle' });
  });

  it('tells the page each time it starts waiting for the commissioner, so the page drops what it had waiting; not when refused', () => {
    createAsCommissioner(page({ reportedGames: [game()] }));
    let started = 0;
    component.started.subscribe(() => started++);

    component.startAdd();
    component.startEdit(game());
    component.requestDelete(game());
    expect(started).toBe(3);

    component.cancel();
    fixture.componentRef.setInput('blocked', true);
    component.startAdd();
    component.startEdit(game());
    component.requestDelete(game());
    expect(started).toBe(3);
  });

  it('while saving, shows what is sent, starts and cancels nothing, and goes back to it when the server refuses', () => {
    createAsCommissioner(page({ reportedGames: [game()] }));
    component.startEdit(game());
    component.save();
    const request = httpMock.expectOne('/api/SeasonMatch/EditManualGame/7/41');
    fixture.detectChanges();
    expect(component.mode()).toEqual({ kind: 'saving', from: { kind: 'entering', target: 41 } });
    expect([component.saving(), component.idle(), component.editing()]).toEqual([true, false, 41]);
    expect(button('Save game')!.disabled).toBeTrue();
    expect(button('Cancel')!.disabled).toBeTrue();

    component.cancel();
    component.startAdd();
    component.requestDelete(game());
    component.save();
    expect(component.mode().kind).toBe('saving');

    request.flush('No such game', { status: 400, statusText: 'Bad Request' });
    expect(component.mode()).toEqual({ kind: 'entering', target: 41 });
    expect(component.error()).toBe('No such game');

    component.requestDelete(game());
    component.confirmDelete();
    expect(component.pendingDelete()).toEqual(game());
    httpMock.expectOne('/api/SeasonMatch/RemoveGame/7/51').flush(null);
    expect(component.mode()).toEqual({ kind: 'idle' });
  });

  it('starts and sends nothing while the page is sending a report or an action of its own', () => {
    createAsCommissioner(page({ reportedGames: [game()] }));
    component.requestDelete(game());
    fixture.componentRef.setInput('blocked', true);
    fixture.detectChanges();
    expect(button('Confirm')!.disabled).toBeTrue();
    component.confirmDelete();
    httpMock.expectNone('/api/SeasonMatch/RemoveGame/7/51');

    component.cancel();
    fixture.detectChanges();
    expect(button('Enter a game by hand')!.disabled).toBeTrue();
    component.startAdd();
    component.startEdit(game());
    component.requestDelete(game());
    expect(component.mode()).toEqual({ kind: 'idle' });

    fixture.componentRef.setInput('blocked', false);
    fixture.detectChanges();
    expect(button('Enter a game by hand')!.disabled).toBeFalse();
  });

  it("takes what decided the match from the server's decidedBy, never from the two results", () => {
    // A BYE decided it: no game is entered, edited or unreported, whatever the results say
    createAsCommissioner(page({ reportedGames: [game()], decidedBy: 'Bye' }));
    expect(component.slots()).toEqual([]);
    expect(component.canRemove(game())).toBeFalse();
    // Decided by its games: a replacement only
    fixture.componentRef.setInput('match', page({ reportedGames: [game()], decidedBy: 'Games' }));
    expect(component.slots().map(choice => choice.slot)).toEqual([51]);
  });

  it('defaults to the first game listed when Halo Infinite is not among them, and copes with no list', () => {
    create(page(), true);
    httpMock.expectOne('/api/game-versions').flush([{ gameVersionID: 2, name: 'Halo Reach' }]);
    component.startAdd();
    expect(component.form.gameVersionID).toBe(2);

    create(page(), true);
    httpMock.expectOne('/api/game-versions').flush(null, { status: 500, statusText: 'Server Error' });
    component.startAdd();
    expect(component.form.gameVersionID).toBe(INFINITE_GAME_VERSION_ID);
  });
});

describe('manual game helpers', () => {
  const form = (overrides: Partial<ManualGameForm> = {}): ManualGameForm => ({
    gameVersionID: 1, map: '', gametype: ' Grifball ', playedAt: '', homeTeamScore: 5, awayTeamScore: 3, players: [], slot: null, ...overrides,
  });

  it('turns a local time into UTC, and leaves out a bad one', () => {
    const local = '2026-01-10T20:30';
    expect(toRequest(form({ playedAt: local })).playedAt).toBe(DateTime.fromISO(local).toUTC().toISO());
    expect(toRequest(form({ playedAt: 'not a time' })).playedAt).toBeNull();
    expect(toRequest(form()).gametype).toBe('Grifball');
  });

  it('fills blank stats with zero and a blank score with none', () => {
    const blank = null as unknown as number;
    const line = { userID: 1, gamertag: 'Grif', isHomeTeam: true, played: true, kills: blank, deaths: 2, assists: blank, goals: blank, score: null };
    const other = { userID: 2, gamertag: 'Tucker', isHomeTeam: false, played: true, kills: 4, deaths: blank, assists: 1, goals: 1, score: 3 };
    expect(toRequest(form({ homeTeamScore: null, awayTeamScore: null, players: [line, other] }))).toEqual(jasmine.objectContaining({
      homeTeamScore: 0,
      awayTeamScore: 0,
      players: [
        { userID: 1, isHomeTeam: true, kills: 0, deaths: 2, assists: 0, goals: 0, score: null },
        { userID: 2, isHomeTeam: false, kills: 4, deaths: 0, assists: 1, goals: 1, score: 3 },
      ],
    }));
  });

  it('shows no time for none or a bad one', () => {
    expect(toLocalInput(null)).toBe('');
    expect(toLocalInput('garbage')).toBe('');
  });
});
