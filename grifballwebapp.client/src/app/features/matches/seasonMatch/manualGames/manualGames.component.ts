import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output } from '@angular/core';
import { outputFromObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import type { TeamPagePlayerDto } from '@api/models';
import { ReportedGameDto, SeasonMatchPageDto } from '../seasonMatchPageDto';
import { decidedOtherwise, defaultGameVersion, emptyForm, formLines, ManualGameForm, slotChoices, toLocalInput } from './manualGameForm';
import { ManualGamesStore } from './manualGames.store';

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
  providers: [ManualGamesStore],
})
export class ManualGamesComponent {
  /** What manual entry is doing, the game versions, and the calls that save. */
  private readonly store = inject(ManualGamesStore);

  readonly seasonMatchID = input.required<number>();
  /** The match, null while the page loads. */
  readonly match = input<SeasonMatchPageDto | null>(null);
  readonly homeRoster = input<TeamPagePlayerDto[]>([]);
  readonly awayRoster = input<TeamPagePlayerDto[]>([]);
  /** The user is a commissioner: may enter, edit and delete games. */
  readonly canEdit = input(false);
  /** The page is sending a report or an action of its own: nothing starts or is sent here meanwhile. */
  readonly blocked = input(false);
  /** Something here now waits for the commissioner (a game in the form, an unreport to confirm): the page drops what it
   * had waiting (a Confirm, a slot choice), so the two never both wait. */
  readonly started = output<void>();
  /** A game was added, changed or deleted: the page reloads the match. */
  readonly changed = outputFromObservable(this.store.saved);

  readonly gameVersions = this.store.gameVersions;
  /** What manual entry is doing (see ManualGamesMode). */
  readonly mode = this.store.mode;
  form: ManualGameForm = emptyForm();
  readonly error = this.store.error;
  /** The game in the form: 'new', the RecordedGameID of the one being edited, or null. */
  readonly editing = this.store.editing;
  /** The game waiting for its unreport to be confirmed, or null. */
  readonly pendingDelete = this.store.pendingDelete;
  /** A game is being sent to the server. */
  readonly saving = this.store.saving;
  /** Nothing else is under way: an entry, a delete waiting to be confirmed, or a save. */
  readonly idle = this.store.idle;

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

  /** A game entered by hand can be edited, unless a forfeit or BYE decided the match. */
  canChange(game: ReportedGameDto | null | undefined): boolean {
    return !!game?.isManual && game.recordedGameID != null && this.canRemove(game);
  }

  /** Any game of the series can be unreported, at any point, unless a forfeit or BYE decided the match. */
  canRemove(game: ReportedGameDto | null | undefined): boolean {
    return !!game && this.canManage() && !this.decidedOtherwise();
  }

  constructor() {
    // The games to pick from, only for commissioners (the list is theirs)
    effect(() => {
      if (this.canManage())
        this.store.loadGameVersions();
    });
  }

  startAdd(): void {
    if (this.saving() || this.blocked())
      return;
    // With games in the series, the commissioner picks whether it is new or replaces one: nothing is picked for them
    const slots = this.slots();
    const only = this.match()?.reportedGames.length === 0 && slots.length === 1 ? slots[0].slot : null;
    this.form = { ...emptyForm(), gameVersionID: defaultGameVersion(this.gameVersions()), players: this.lines(null), slot: only };
    this.store.enter('new');
    this.started.emit();
  }

  /** Edits a game of the box score, the form filled in from it. */
  startEdit(game: ReportedGameDto): void {
    if (!this.canChange(game) || this.saving() || this.blocked())
      return;
    this.form = {
      gameVersionID: game.gameVersionID ?? defaultGameVersion(this.gameVersions()),
      map: game.map ?? '',
      gametype: game.gametype ?? '',
      playedAt: toLocalInput(game.playedAt ?? null),
      homeTeamScore: game.homeScore ?? null,
      awayTeamScore: game.awayScore ?? null,
      players: this.lines(game),
      slot: null,
    };
    this.store.enter(game.recordedGameID!);
    this.started.emit();
  }

  /** Closes the form or the unreport waiting for its Confirm; nothing while one is being sent. */
  cancel(): void {
    this.store.cancel();
  }

  /** The scores are in and differ (the side with the higher one won); a new game's place in the series is picked. */
  formValid(): boolean {
    const { homeTeamScore: home, awayTeamScore: away } = this.form;
    return home != null && away != null && home >= 0 && away >= 0 && home !== away
      && (this.editing() !== 'new' || this.form.slot !== null);
  }

  /** Sends the game in the form; done, manual entry is idle and the page reloads; refused, it stays with why. */
  save(): void {
    if (this.mode().kind !== 'entering' || !this.formValid() || this.blocked())
      return;
    this.store.save(this.seasonMatchID(), this.form);
  }

  /** Asks to confirm unreporting a game of the series. */
  requestDelete(game: ReportedGameDto): void {
    if (!this.canRemove(game) || this.saving() || this.blocked())
      return;
    this.store.askDelete(game);
    this.started.emit();
  }

  confirmDelete(): void {
    if (this.blocked())
      return;
    this.store.confirmDelete(this.seasonMatchID());
  }

  /** A line per roster player, filled in from the game being edited; its players not on a roster (subs) too. */
  private lines(game: ReportedGameDto | null) {
    return formLines(game, this.homeRoster(), this.awayRoster());
  }
}

