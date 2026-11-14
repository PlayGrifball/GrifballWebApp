import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReportedGameDto } from '../seasonMatchPageDto';
import { emptyForm, ManualGameForm } from './manualGameForm';
import { ManualGamesStore } from './manualGames.store';

describe('ManualGamesStore', () => {
  let store: ManualGamesStore;
  let http: HttpTestingController;
  let saved: number;

  const form = (): ManualGameForm => ({ ...emptyForm(), homeTeamScore: 5, awayTeamScore: 2, slot: 'new' });
  const game = { seriesGameID: 51, recordedGameID: 41, matchNumber: 1 } as ReportedGameDto;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ManualGamesStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(ManualGamesStore);
    http = TestBed.inject(HttpTestingController);
    saved = 0;
    store.saved.subscribe(() => saved++);
  });

  afterEach(() => http.verify());

  it('asks for the game versions once, and keeps none when that fails', () => {
    store.loadGameVersions();
    store.loadGameVersions();
    http.expectOne('/api/game-versions').flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.gameVersions()).toEqual([]);
  });

  it('adds a new game: saving while it is sent, then idle and saved', () => {
    store.enter('new');
    expect(store.editing()).toBe('new');
    store.save(7, form());
    expect(store.saving()).toBeTrue();
    expect(store.editing()).toBe('new');
    const request = http.expectOne({ method: 'POST', url: '/api/season-matches/7/manual-games' });
    expect(request.request.body).toEqual(jasmine.objectContaining({ homeTeamScore: 5, awayTeamScore: 2, replacesSeriesGameID: null }));
    request.flush(null);
    expect(store.idle()).toBeTrue();
    expect(saved).toBe(1);
  });

  it('changes the game edited, never moving it to another slot', () => {
    store.enter(41);
    store.save(7, { ...form(), slot: 51 });
    const request = http.expectOne({ method: 'POST', url: '/api/season-matches/7/manual-games/41' });
    expect(request.request.body.replacesSeriesGameID).toBeNull();
    request.flush(null);
    expect(saved).toBe(1);
  });

  it('goes back to what it sent, with why, when the server refuses it', () => {
    store.askDelete(game);
    store.confirmDelete(7);
    expect(store.pendingDelete()).toBe(game);
    http.expectOne({ method: 'POST', url: '/api/season-matches/7/games/51/remove' })
      // A ProblemDetails, as text (the call reads its answer as text)
      .flush('{"title":"Request refused","detail":"No such game"}', { status: 400, statusText: 'Bad Request' });
    expect(store.mode()).toEqual({ kind: 'confirmDelete', game });
    expect(store.error()).toBe('No such game');
    expect(saved).toBe(0);
  });

  it('sends nothing that is not waiting, and cancels nothing while sending', () => {
    store.save(7, form());
    store.confirmDelete(7);
    http.expectNone(() => true);

    store.enter('new');
    store.save(7, form());
    store.cancel();
    expect(store.saving()).toBeTrue();
    http.expectOne('/api/season-matches/7/manual-games').flush(null);
  });
});
