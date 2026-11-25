import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import type { GameVersionOptionDto } from '@api/models';
import { SeasonMatchPageDto } from '../seasonMatchPageDto';
import { GameSlot, isFormValid, ManualGameForm, SlotChoice } from './manualGameForm';

/**
 * The form a commissioner enters or edits a game by hand in: the score, the game's place in the series, and each
 * player's stats. Its own component, so its code (form fields, selects, checkboxes) is loaded only when a commissioner
 * opens it (ManualGamesComponent defers it), not for every visitor of the match page. The form is the caller's: the
 * fields edit it in place, and the caller saves it.
 */
@Component({
  selector: 'app-manual-game-editor',
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './manualGameEditor.component.html',
  styleUrl: './manualGameEditor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManualGameEditorComponent {
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
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  /** The scores are in and differ; a new game's place in the series is picked. */
  valid(): boolean {
    return isFormValid(this.form(), this.editing());
  }

  submit(): void {
    if (this.valid())
      this.saved.emit();
  }
}
