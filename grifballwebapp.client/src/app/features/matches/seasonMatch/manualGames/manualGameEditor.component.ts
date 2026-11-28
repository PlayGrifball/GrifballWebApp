import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, OnInit, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import type { GameVersionOptionDto } from '@api/models';
import { SeasonMatchPageDto } from '../seasonMatchPageDto';
import { GameSlot, isFormValid, ManualGameForm, PlayerLine, scoresTie, SlotChoice } from './manualGameForm';

/** A player line's editable fields; its identity (userID, gamertag, isHomeTeam) never changes here, so it isn't one. */
type PlayerLineGroup = FormGroup<{
  played: FormControl<boolean>;
  kills: FormControl<number | null>;
  deaths: FormControl<number | null>;
  assists: FormControl<number | null>;
  goals: FormControl<number | null>;
  score: FormControl<number | null>;
}>;

/** The game's own FormGroup: the scores, a new game's place in the series, its details, and the player lines. */
type ManualGameFormGroup = FormGroup<{
  homeTeamScore: FormControl<number | null>;
  awayTeamScore: FormControl<number | null>;
  slot: FormControl<GameSlot | null>;
  gameVersionID: FormControl<number>;
  map: FormControl<string>;
  gametype: FormControl<string>;
  playedAt: FormControl<string>;
  players: FormArray<PlayerLineGroup>;
}>;

/**
 * The form a commissioner enters or edits a game by hand in: the score, the game's place in the series, and each
 * player's stats. Its own component, so its code (form fields, selects, checkboxes) is loaded only when a commissioner
 * opens it (ManualGamesComponent defers it), not for every visitor of the match page. ManualGamesComponent recreates
 * the editor (never reuses it with a changed `form`) for a different target, so its FormGroup is built once, here,
 * from the `form` it started with; the edited game is emitted on save, never written back into the caller's object.
 */
@Component({
  selector: 'app-manual-game-editor',
  imports: [ReactiveFormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './manualGameEditor.component.html',
  styleUrl: './manualGameEditor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualGameEditorComponent implements OnInit {
  readonly form = input.required<ManualGameForm>();
  /** The game in the form: 'new', or the RecordedGameID of the one being edited. */
  readonly editing = input.required<GameSlot>();
  /** The match, null while the page loads. */
  readonly match = input<SeasonMatchPageDto | null>(null);
  /** Where a new game can go (a new game, or in place of one reported). */
  readonly slots = input<SlotChoice[]>([]);
  readonly gameVersions = input<GameVersionOptionDto[]>([]);
  /** The game is being sent. */
  readonly saving = input(false);
  /** The page is sending a report or an action of its own: nothing is sent from here meanwhile. */
  readonly blocked = input(false);

  /** Save the game in the form (it is valid). */
  readonly saved = output<ManualGameForm>();
  readonly cancelled = output<void>();

  private readonly destroyRef = inject(DestroyRef);

  /** The game the editor started with, and its FormGroup: both built once, in ngOnInit (a required input has no
   * value yet in a field initializer or the constructor — NG8118 — so they can't be built there). */
  protected initial!: ManualGameForm;
  protected formGroup!: ManualGameFormGroup;

  ngOnInit(): void {
    this.initial = this.form();
    this.formGroup = new FormGroup({
      homeTeamScore: new FormControl<number | null>(this.initial.homeTeamScore),
      awayTeamScore: new FormControl<number | null>(this.initial.awayTeamScore),
      slot: new FormControl<GameSlot | null>(this.initial.slot),
      gameVersionID: new FormControl(this.initial.gameVersionID, { nonNullable: true }),
      map: new FormControl(this.initial.map, { nonNullable: true }),
      gametype: new FormControl(this.initial.gametype, { nonNullable: true }),
      playedAt: new FormControl(this.initial.playedAt, { nonNullable: true }),
      players: new FormArray(this.initial.players.map(line => this.playerLineGroup(line))),
    });
    // A line's stats are entered only once it is ticked as played; its control starts disabled to match
    for (const line of this.formGroup.controls.players.controls)
      line.controls.played.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(played => this.setPlayed(line, played));
  }

  /** The scores are in and differ; a new game's place in the series is picked. Reads the three controls it needs
   * straight from the FormGroup, not the full (player-rebuilding) {@link value}. */
  valid(): boolean {
    const { homeTeamScore, awayTeamScore, slot } = this.formGroup.controls;
    return isFormValid({ ...this.initial, homeTeamScore: homeTeamScore.value, awayTeamScore: awayTeamScore.value, slot: slot.value }, this.editing());
  }

  /** The scores are in and equal: a game needs a winner (the same rule {@link isFormValid} checks, as {@link scoresTie}). */
  protected tied(): boolean {
    const { homeTeamScore, awayTeamScore } = this.formGroup.controls;
    return scoresTie({ homeTeamScore: homeTeamScore.value, awayTeamScore: awayTeamScore.value });
  }

  submit(): void {
    if (this.valid())
      this.saved.emit(this.value());
  }

  private playerLineGroup(line: PlayerLine): PlayerLineGroup {
    const group = new FormGroup({
      played: new FormControl(line.played, { nonNullable: true }),
      kills: new FormControl<number | null>(line.kills),
      deaths: new FormControl<number | null>(line.deaths),
      assists: new FormControl<number | null>(line.assists),
      goals: new FormControl<number | null>(line.goals),
      score: new FormControl<number | null>(line.score),
    });
    this.setPlayed(group, line.played);
    return group;
  }

  /** A line's stats can be entered only while it is ticked as played; disabled, they keep their last value. */
  private setPlayed(line: PlayerLineGroup, played: boolean): void {
    const stats = [line.controls.kills, line.controls.deaths, line.controls.assists, line.controls.goals, line.controls.score];
    for (const stat of stats) {
      if (played)
        stat.enable({ emitEvent: false });
      else
        stat.disable({ emitEvent: false });
    }
  }

  /** The form as it stands: the caller's player lines (their identity, never edited here) with the edited fields
   * overlaid; a stat cleared to nothing (the number input's empty value is null) counts as 0, as it is sent. */
  private value(): ManualGameForm {
    const raw = this.formGroup.getRawValue();
    return {
      ...raw,
      players: this.initial.players.map((line, i) => {
        const edited = raw.players[i];
        return {
          ...line,
          played: edited.played,
          kills: edited.kills ?? 0,
          deaths: edited.deaths ?? 0,
          assists: edited.assists ?? 0,
          goals: edited.goals ?? 0,
          score: edited.score,
        };
      }),
    };
  }
}
