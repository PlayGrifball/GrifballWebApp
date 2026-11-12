import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { DateTime } from 'luxon';
import { ApiClientService } from '../../../api/apiClient.service';
import type { GameVersionOptionDto } from '@api/models';
import { errorText } from '../../../shared/errorText';
import { PlayerDto } from '../../team/team.component';
import { ManualGameRequestDto, ReportedGameDto, SeasonMatchGameParticipantDto, SeasonMatchPageDto } from '../seasonMatchPageDto';

/** Halo Infinite (seeded): what a game is entered as unless the commissioner picks another. */
export const INFINITE_GAME_VERSION_ID = 6;

/** One player's line in the form: a roster player (or one already entered), counted only when played is ticked. */
export interface PlayerLine {
  userID: number,
  gamertag: string,
  isHomeTeam: boolean,
  played: boolean,
  kills: number,
  deaths: number,
  assists: number,
  /** Grifball goals (bomb scores). */
  goals: number,
  score: number | null,
}

/**
 * Where a game goes in the series, as the commissioner picks it (never guessed): a new game, or in place of a game
 * already reported (its SeriesGameID), one this game corrects.
 */
export type GameSlot = 'new' | number;

/** A choice of {@link GameSlot}, as offered. */
export interface SlotChoice {
  slot: GameSlot,
  label: string,
}

/**
 * Where a game being reported or entered can go: a new game while the series is undecided and has room, and in place of
 * each game it has. A forfeit or BYE decided the match, not its games: nothing then (Undo Result first).
 */
export function slotChoices(match: SeasonMatchPageDto | null): SlotChoice[] {
  if (match === null || decidedOtherwise(match))
    return [];
  const games = match.reportedGames;
  const choices: SlotChoice[] = [];
  if (!match.decidedBy && games.length < match.bestOf)
    choices.push({ slot: 'new', label: `A new game (game ${games.length + 1})` });
  for (const game of games)
    choices.push({ slot: game.seriesGameID, label: `Replaces game ${game.matchNumber} (${gameSource(game)})` });
  return choices;
}

/** A forfeit or a BYE decided the match, not its games (the server's decidedBy): those are undone first (Undo Result). */
function decidedOtherwise(match: SeasonMatchPageDto): boolean {
  return match.decidedBy === 'Forfeit' || match.decidedBy === 'Bye';
}

/** Where a game of the series came from, as the choices and the unreport confirmation say it. */
export function gameSource(game: ReportedGameDto): string {
  if (game.matchID)
    return 'from Waypoint';
  return game.isManual ? 'entered by hand' : 'recorded';
}

/** What the form holds; playedAt is the datetime-local input's value (local time), '' for none. */
export interface ManualGameForm {
  gameVersionID: number,
  map: string,
  gametype: string,
  playedAt: string,
  homeTeamScore: number | null,
  awayTeamScore: number | null,
  players: PlayerLine[],
  /** A new game's place in the series, as picked; null until picked (a series with no games has only one). */
  slot: GameSlot | null,
}

/** A game being entered by hand ('new') or edited (its RecordedGameID), in the form. */
type Entering = { kind: 'entering', target: 'new' | number };
/** A game of the series waiting for its unreport to be confirmed. */
type ConfirmingDelete = { kind: 'confirmDelete', game: ReportedGameDto };

/**
 * What manual entry is doing, one thing at a time: nothing; a game in the form (new, or one being edited); an unreport
 * waiting for its Confirm; or sending one of those (`from`, shown meanwhile and gone back to if the server refuses it).
 */
export type ManualGamesMode =
  | { kind: 'idle' }
  | Entering
  | ConfirmingDelete
  | { kind: 'saving', from: Entering | ConfirmingDelete };

const IDLE: ManualGamesMode = { kind: 'idle' };

/**
 * Manual match entry on the match page: a commissioner enters a game by hand (the stat bot or Halo Infinite's stats
 * missed it, or it was not played in Halo Infinite), with the score and, optionally, each player's kills, deaths,
 * assists, goals and score; and edits or deletes the games entered so. The games themselves show in the page's box
 * score, whose Edit and Delete buttons call {@link startEdit} and {@link requestDelete} here.
 */
@Component({
  selector: 'app-manual-games',
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './manualGames.component.html',
  styleUrl: './manualGames.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualGamesComponent {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiClientService);

  readonly seasonMatchID = input.required<number>();
  /** The match, null while the page loads. */
  readonly match = input<SeasonMatchPageDto | null>(null);
  readonly homeRoster = input<PlayerDto[]>([]);
  readonly awayRoster = input<PlayerDto[]>([]);
  /** The user is a commissioner: may enter, edit and delete games. */
  readonly canEdit = input(false);
  /** The page is sending a report or an action of its own: nothing starts or is sent here meanwhile. */
  readonly blocked = input(false);
  /** Something here now waits for the commissioner (a game in the form, an unreport to confirm): the page drops what it
   * had waiting (a Confirm, a slot choice), so the two never both wait. */
  readonly started = output<void>();
  /** A game was added, changed or deleted: the page reloads the match. */
  readonly changed = output<void>();

  readonly gameVersions = signal<GameVersionOptionDto[]>([]);

  /** What manual entry is doing (see {@link ManualGamesMode}). Changed only by the methods below. */
  private readonly _mode = signal<ManualGamesMode>(IDLE);
  readonly mode = this._mode.asReadonly();
  form: ManualGameForm = emptyForm();
  readonly error = signal<string | null>(null);

  /** What is shown, while it waits or is being sent. */
  private readonly shown = computed(() => { const mode = this.mode(); return mode.kind === 'saving' ? mode.from : mode; });
  /** The game in the form: 'new', the RecordedGameID of the one being edited, or null. */
  readonly editing = computed(() => { const shown = this.shown(); return shown.kind === 'entering' ? shown.target : null; });
  /** The game waiting for its unreport to be confirmed, or null. */
  readonly pendingDelete = computed(() => { const shown = this.shown(); return shown.kind === 'confirmDelete' ? shown.game : null; });
  /** A game is being sent to the server. */
  readonly saving = computed(() => this.mode().kind === 'saving');

  /** A forfeit or a BYE decided the match, not its games: those are undone first (Undo Result). */
  private readonly decidedOtherwise = computed(() => { const m = this.match(); return m !== null && decidedOtherwise(m); });

  /** Games can be entered by hand for this match at all: both teams, not an imported season. */
  readonly canManage = computed(() => {
    const m = this.match();
    return this.canEdit() && m !== null && !m.isImported && m.homeTeamID != null && m.awayTeamID != null;
  });

  /** Where a game entered now can go (a new game, or in place of one reported): see {@link slotChoices}. */
  readonly slots = computed(() => slotChoices(this.match()));

  /** A game can be entered: as a new game, or replacing one of the series. Every game of the series, Waypoint and
   * recorded alike, is in reportedGames. */
  readonly canAdd = computed(() => this.canManage() && this.slots().length > 0);

  /** Nothing else is under way: an entry, a delete waiting to be confirmed, or a save. */
  readonly idle = computed(() => this.mode().kind === 'idle');

  /** A game entered by hand can be edited, unless a forfeit or BYE decided the match. */
  canChange(game: ReportedGameDto | null | undefined): boolean {
    return !!game?.isManual && game.recordedGameID != null && this.canRemove(game);
  }

  /** Any game of the series can be unreported, at any point, unless a forfeit or BYE decided the match. */
  canRemove(game: ReportedGameDto | null | undefined): boolean {
    return !!game && this.canManage() && !this.decidedOtherwise();
  }

  /** The game versions have been asked for (once: a failure leaves the default). */
  private versionsRequested = false;

  constructor() {
    // The games to pick from, only for commissioners (the list is theirs)
    effect(() => {
      if (!this.canManage() || this.versionsRequested)
        return;
      this.versionsRequested = true;
      this.api.getGameVersions().subscribe({
        next: versions => this.gameVersions.set(versions ?? []),
        error: () => this.gameVersions.set([]),
      });
    });
  }

  startAdd(): void {
    if (this.saving() || this.blocked())
      return;
    this.error.set(null);
    // With games in the series, the commissioner picks whether it is new or replaces one: nothing is picked for them
    const slots = this.slots();
    const only = this.match()?.reportedGames.length === 0 && slots.length === 1 ? slots[0].slot : null;
    this.form = { ...emptyForm(), gameVersionID: this.defaultGameVersion(), players: this.lines(null), slot: only };
    this._mode.set({ kind: 'entering', target: 'new' });
    this.started.emit();
  }

  /** Edits a game of the box score, the form filled in from it. */
  startEdit(game: ReportedGameDto): void {
    if (!this.canChange(game) || this.saving() || this.blocked())
      return;
    this.error.set(null);
    this.form = {
      gameVersionID: game.gameVersionID ?? this.defaultGameVersion(),
      map: game.map ?? '',
      gametype: game.gametype ?? '',
      playedAt: toLocalInput(game.playedAt ?? null),
      homeTeamScore: game.homeScore ?? null,
      awayTeamScore: game.awayScore ?? null,
      players: this.lines(game),
      slot: null,
    };
    this._mode.set({ kind: 'entering', target: game.recordedGameID! });
    this.started.emit();
  }

  /** Closes the form or the unreport waiting for its Confirm; nothing while one is being sent. */
  cancel(): void {
    if (this.saving())
      return;
    this._mode.set(IDLE);
    this.error.set(null);
  }

  /** The scores are in and differ (the side with the higher one won); a new game's place in the series is picked. */
  formValid(): boolean {
    const { homeTeamScore: home, awayTeamScore: away } = this.form;
    return home != null && away != null && home >= 0 && away >= 0 && home !== away
      && (this.editing() !== 'new' || this.form.slot !== null);
  }

  save(): void {
    const mode = this.mode();
    if (mode.kind !== 'entering' || !this.formValid())
      return;
    const editing = mode.target;
    const body = editing === 'new' ? toRequest(this.form) : { ...toRequest(this.form), replacesSeriesGameID: null };
    const url = editing === 'new'
      ? `/api/season-matches/${this.seasonMatchID()}/manual-games`
      : `/api/season-matches/${this.seasonMatchID()}/manual-games/${editing}`;
    this.send(mode, url, body);
  }

  /** Asks to confirm unreporting a game of the series. */
  requestDelete(game: ReportedGameDto): void {
    if (!this.canRemove(game) || this.saving() || this.blocked())
      return;
    this.error.set(null);
    this._mode.set({ kind: 'confirmDelete', game });
    this.started.emit();
  }

  confirmDelete(): void {
    const mode = this.mode();
    if (mode.kind !== 'confirmDelete')
      return;
    this.send(mode, `/api/season-matches/${this.seasonMatchID()}/games/${mode.game.seriesGameID}/remove`, null);
  }

  /** Sends what is shown: done, manual entry is idle and the page reloads; refused, it is shown again with why. */
  private send(from: Entering | ConfirmingDelete, url: string, body: unknown): void {
    if (this.blocked())
      return;
    this._mode.set({ kind: 'saving', from });
    this.error.set(null);
    this.http.post(url, body).subscribe({
      next: () => {
        this._mode.set(IDLE);
        this.changed.emit();
      },
      error: (e: HttpErrorResponse) => {
        this._mode.set(from);
        this.error.set(errorText(e, 'The server could not save that.'));
      },
    });
  }

  private defaultGameVersion(): number {
    const versions = this.gameVersions();
    return versions.some(v => v.gameVersionID === INFINITE_GAME_VERSION_ID) || versions.length === 0
      ? INFINITE_GAME_VERSION_ID
      : versions[0].gameVersionID;
  }

  /** A line per roster player, filled in from the game being edited; its players not on a roster (subs) too. */
  private lines(game: ReportedGameDto | null): PlayerLine[] {
    const line = (p: SeasonMatchGameParticipantDto, isHomeTeam: boolean): PlayerLine => ({
      userID: p.userID!, gamertag: p.gamertag, isHomeTeam, played: true,
      kills: p.kills, deaths: p.deaths, assists: p.assists, goals: p.bombScores, score: p.score,
    });
    // A game entered by hand has a user on every line
    const entered = new Map([
      ...(game?.homePlayers ?? []).filter(p => p.userID != null).map(p => [p.userID!, line(p, true)] as const),
      ...(game?.awayPlayers ?? []).filter(p => p.userID != null).map(p => [p.userID!, line(p, false)] as const),
    ]);
    const roster = (players: PlayerDto[], isHomeTeam: boolean): PlayerLine[] => players.map(p => {
      const entry = entered.get(p.userID);
      entered.delete(p.userID);
      return entry
        ?? { userID: p.userID, gamertag: p.gamertag ?? 'Unknown Spartan', isHomeTeam, played: false, kills: 0, deaths: 0, assists: 0, goals: 0, score: null };
    });
    const lines = [...roster(this.homeRoster(), true), ...roster(this.awayRoster(), false)];
    for (const sub of entered.values())
      lines.push(sub);
    return lines;
  }
}

function emptyForm(): ManualGameForm {
  return { gameVersionID: INFINITE_GAME_VERSION_ID, map: '', gametype: '', playedAt: '', homeTeamScore: null, awayTeamScore: null, players: [], slot: null };
}

/** The form as the API takes it: the ticked players only, the time in UTC. */
export function toRequest(form: ManualGameForm): ManualGameRequestDto {
  const playedAt = form.playedAt ? DateTime.fromISO(form.playedAt) : null;
  return {
    gameVersionID: form.gameVersionID,
    map: form.map.trim() || null,
    gametype: form.gametype.trim() || null,
    playedAt: playedAt?.isValid ? playedAt.toUTC().toISO() : null,
    homeTeamScore: form.homeTeamScore ?? 0,
    awayTeamScore: form.awayTeamScore ?? 0,
    replacesSeriesGameID: typeof form.slot === 'number' ? form.slot : null,
    players: form.players.filter(p => p.played).map(p => ({
      userID: p.userID,
      isHomeTeam: p.isHomeTeam,
      kills: p.kills ?? 0,
      deaths: p.deaths ?? 0,
      assists: p.assists ?? 0,
      goals: p.goals ?? 0,
      score: p.score,
    })),
  };
}

/** An API time as a datetime-local input's value (local time), '' for none. */
export function toLocalInput(iso: string | null): string {
  if (!iso)
    return '';
  const time = DateTime.fromISO(iso);
  return time.isValid ? time.toLocal().toFormat("yyyy-MM-dd'T'HH:mm") : '';
}
