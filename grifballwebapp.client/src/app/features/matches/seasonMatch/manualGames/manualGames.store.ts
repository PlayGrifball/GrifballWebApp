import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import type { GameVersionOptionDto } from '@api/models';
import { EventOrganizerService } from '@api/services/event-organizer.service';
import { SeasonMatchService } from '@api/services/season-match.service';
import { errorText } from '@shared/errorText';
import { Observable, Subject } from 'rxjs';
import { ReportedGameDto } from '../seasonMatchPageDto';
import { ManualGameForm, toRequest } from './manualGameForm';

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
 * Manual entry's state and server calls: what it is doing (see {@link ManualGamesMode}), why the server last refused,
 * the game versions to pick from, and the add, edit and unreport calls. One per manual entry (the component provides it).
 */
@Injectable()
export class ManualGamesStore {
  private readonly eventOrganizer = inject(EventOrganizerService);
  private readonly seasonMatch = inject(SeasonMatchService);

  private readonly _gameVersions = signal<GameVersionOptionDto[]>([]);
  readonly gameVersions = this._gameVersions.asReadonly();
  /** The game versions have been asked for (once: a failure leaves the default). */
  private versionsRequested = false;

  /** What manual entry is doing. Changed only by the methods below. */
  private readonly _mode = signal<ManualGamesMode>(IDLE);
  readonly mode = this._mode.asReadonly();
  private readonly _error = signal<string | null>(null);
  /** Why the server refused the last save or unreport. */
  readonly error = this._error.asReadonly();

  /** What is shown, while it waits or is being sent. */
  private readonly shown = computed(() => { const mode = this.mode(); return mode.kind === 'saving' ? mode.from : mode; });
  /** The game in the form: 'new', the RecordedGameID of the one being edited, or null. */
  readonly editing = computed(() => { const shown = this.shown(); return shown.kind === 'entering' ? shown.target : null; });
  /** The game waiting for its unreport to be confirmed, or null. */
  readonly pendingDelete = computed(() => { const shown = this.shown(); return shown.kind === 'confirmDelete' ? shown.game : null; });
  /** A game is being sent to the server. */
  readonly saving = computed(() => this.mode().kind === 'saving');
  /** Nothing else is under way: an entry, a delete waiting to be confirmed, or a save. */
  readonly idle = computed(() => this.mode().kind === 'idle');

  private readonly _saved = new Subject<void>();
  /** A game was added, changed or unreported. */
  readonly saved = this._saved.asObservable();

  /** Asks for the game versions to pick from, the first time only. */
  loadGameVersions(): void {
    if (this.versionsRequested)
      return;
    this.versionsRequested = true;
    this.eventOrganizer.getGameVersions().subscribe({
      next: versions => this._gameVersions.set(versions ?? []),
      error: () => this._gameVersions.set([]),
    });
  }

  /** A game in the form: a new one, or the one with this RecordedGameID. */
  enter(target: 'new' | number): void {
    this._error.set(null);
    this._mode.set({ kind: 'entering', target });
  }

  /** Asks to confirm unreporting a game of the series. */
  askDelete(game: ReportedGameDto): void {
    this._error.set(null);
    this._mode.set({ kind: 'confirmDelete', game });
  }

  /** Closes the form or the unreport waiting for its Confirm; nothing while one is being sent. */
  cancel(): void {
    if (this.saving())
      return;
    this._mode.set(IDLE);
    this._error.set(null);
  }

  /** Sends the game in the form: added as a new game (or in place of the one its slot names), or the one edited changed. */
  save(seasonMatchID: number, form: ManualGameForm): void {
    const mode = this.mode();
    if (mode.kind !== 'entering')
      return;
    const editing = mode.target;
    const request = editing === 'new'
      ? this.seasonMatch.addManualGame({ seasonMatchID, body: toRequest(form) })
      : this.seasonMatch.editManualGame({ seasonMatchID, recordedGameID: editing, body: { ...toRequest(form), replacesSeriesGameID: null } });
    this.send(mode, request);
  }

  /** Unreports the game waiting for its Confirm. */
  confirmDelete(seasonMatchID: number): void {
    const mode = this.mode();
    if (mode.kind !== 'confirmDelete')
      return;
    this.send(mode, this.seasonMatch.removeGame({ seasonMatchID, seriesGameID: mode.game.seriesGameID }));
  }

  /** Sends what is shown: done, manual entry is idle and {@link saved} says so; refused, it is shown again with why. */
  private send(from: Entering | ConfirmingDelete, request: Observable<unknown>): void {
    this._mode.set({ kind: 'saving', from });
    this._error.set(null);
    request.subscribe({
      next: () => {
        this._mode.set(IDLE);
        this._saved.next();
      },
      error: (e: HttpErrorResponse) => {
        this._mode.set(from);
        this._error.set(errorText(e, 'The server could not save that.'));
      },
    });
  }
}
