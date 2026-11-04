import { DatePipe } from '@angular/common';
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
import { GameVersionDto } from '../../../api/dtos/optionDtos';
import { errorText } from '../../../shared/errorText';
import { PlayerDto } from '../../team/team.component';
import { ManualGameRequestDto, RecordedSeriesGameDto, SeasonMatchPageDto } from '../seasonMatchPageDto';

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
  score: number | null,
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
}

/**
 * The series' recorded games (not Waypoint matches) on the match page, and manual match entry: a commissioner enters a
 * game by hand (the stat bot or Halo Infinite's stats missed it, or it was not played in Halo Infinite), with the score
 * and, optionally, each player's kills, deaths, assists and score; and edits or deletes the games entered so.
 */
@Component({
  selector: 'app-recorded-games',
  imports: [DatePipe, FormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './recordedGames.component.html',
  styleUrl: './recordedGames.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecordedGamesComponent {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiClientService);

  readonly seasonMatchID = input.required<number>();
  readonly match = input.required<SeasonMatchPageDto>();
  readonly homeRoster = input<PlayerDto[]>([]);
  readonly awayRoster = input<PlayerDto[]>([]);
  /** The user is a commissioner: may enter, edit and delete games. */
  readonly canEdit = input(false);
  /** A game was added, changed or deleted: the page reloads the match. */
  readonly changed = output<void>();

  readonly games = computed(() => this.match().recordedGames ?? []);
  readonly gameVersions = signal<GameVersionDto[]>([]);

  /** The game being entered ('new'), the RecordedGameID of the one being edited, or null. */
  readonly editing = signal<'new' | number | null>(null);
  form: ManualGameForm = emptyForm();
  /** The game waiting for its delete to be confirmed. */
  readonly pendingDelete = signal<number | null>(null);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  /** A forfeit or a BYE decided the match, not its games: those are undone first (Undo Result). */
  private readonly decidedOtherwise = computed(() => {
    const m = this.match();
    return [m.homeTeamResult, m.awayTeamResult].some(r => r === 'Forfeit' || r === 'Bye');
  });

  /** Games can be entered by hand for this match at all: both teams, not an imported season. */
  readonly canManage = computed(() => {
    const m = this.match();
    return this.canEdit() && !m.isImported && m.homeTeamID != null && m.awayTeamID != null;
  });

  /** Another game can be added: undecided, and the series has room for it. */
  readonly canAdd = computed(() => {
    const m = this.match();
    return this.canManage() && m.homeTeamResult === 'TBD' && m.awayTeamResult === 'TBD'
      && m.reportedGames.length + this.games().length < m.bestOf;
  });

  /** A game entered by hand can be edited or deleted here, unless a forfeit or BYE decided the match. */
  canChange(game: RecordedSeriesGameDto): boolean {
    return this.canManage() && game.isManual && !this.decidedOtherwise();
  }

  /** The game, map and gametype, as one line. */
  details(game: RecordedSeriesGameDto): string {
    return [game.gameVersionName, game.map, game.gametype].filter(x => !!x).join(' · ');
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
    this.error.set(null);
    this.form = { ...emptyForm(), gameVersionID: this.defaultGameVersion(), players: this.lines(null) };
    this.editing.set('new');
  }

  startEdit(game: RecordedSeriesGameDto): void {
    this.error.set(null);
    this.form = {
      gameVersionID: game.gameVersionID,
      map: game.map ?? '',
      gametype: game.gametype ?? '',
      playedAt: toLocalInput(game.playedAt),
      homeTeamScore: game.homeTeamScore,
      awayTeamScore: game.awayTeamScore,
      players: this.lines(game),
    };
    this.editing.set(game.recordedGameID);
  }

  cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  /** The scores are in and differ: the side with the higher one won. */
  formValid(): boolean {
    const { homeTeamScore: home, awayTeamScore: away } = this.form;
    return home != null && away != null && home >= 0 && away >= 0 && home !== away;
  }

  save(): void {
    const editing = this.editing();
    if (editing === null || !this.formValid())
      return;
    const body = toRequest(this.form);
    const url = editing === 'new'
      ? `/api/SeasonMatch/AddManualGame/${this.seasonMatchID()}`
      : `/api/SeasonMatch/EditManualGame/${this.seasonMatchID()}/${editing}`;
    this.send(url, body, () => this.editing.set(null));
  }

  requestDelete(game: RecordedSeriesGameDto): void {
    this.error.set(null);
    this.pendingDelete.set(game.recordedGameID);
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const id = this.pendingDelete();
    if (id === null)
      return;
    this.send(`/api/SeasonMatch/DeleteManualGame/${this.seasonMatchID()}/${id}`, null, () => this.pendingDelete.set(null));
  }

  private send(url: string, body: unknown, done: () => void): void {
    this.saving.set(true);
    this.error.set(null);
    this.http.post(url, body).subscribe({
      next: () => {
        this.saving.set(false);
        done();
        this.changed.emit();
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
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
  private lines(game: RecordedSeriesGameDto | null): PlayerLine[] {
    const entered = new Map((game?.players ?? []).map(p => [p.userID, p]));
    const roster = (players: PlayerDto[], isHomeTeam: boolean): PlayerLine[] => players.map(p => {
      const line = entered.get(p.userID);
      entered.delete(p.userID);
      return line
        ? { ...line, played: true }
        : { userID: p.userID, gamertag: p.gamertag ?? 'Unknown Spartan', isHomeTeam, played: false, kills: 0, deaths: 0, assists: 0, score: null };
    });
    const lines = [...roster(this.homeRoster(), true), ...roster(this.awayRoster(), false)];
    for (const sub of entered.values())
      lines.push({ ...sub, played: true });
    return lines;
  }
}

function emptyForm(): ManualGameForm {
  return { gameVersionID: INFINITE_GAME_VERSION_ID, map: '', gametype: '', playedAt: '', homeTeamScore: null, awayTeamScore: null, players: [] };
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
    players: form.players.filter(p => p.played).map(p => ({
      userID: p.userID,
      isHomeTeam: p.isHomeTeam,
      kills: p.kills ?? 0,
      deaths: p.deaths ?? 0,
      assists: p.assists ?? 0,
      score: p.score ?? null,
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
