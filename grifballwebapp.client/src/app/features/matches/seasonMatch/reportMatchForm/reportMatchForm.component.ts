import { ChangeDetectionStrategy, Component, input, model, output } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { regexMatchValidValidator } from '@shared/validation/validators/regexMatchValidator';
import { GUID_ERROR_MESSAGE, GUID_PATTERN } from '@shared/validation/guid';

/**
 * A commissioner reports a game of the match by its Waypoint match ID. Its own component, so its code (a form field,
 * forms, validation) is loaded only for commissioners (the match page defers it), not for every visitor of the page.
 */
@Component({
  selector: 'app-report-match-form',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, ErrorMessageComponent],
  template: `
    <form class="app-form" (ngSubmit)="submit()">
      <mat-form-field appearance="outline" class="app-full-width">
        <mat-label>Match ID</mat-label>
        <input matInput [formControl]="matchIDControl" name="MatchID">
        <app-error-message [control]="matchIDControl" />
      </mat-form-field>
      <button mat-flat-button [disabled]="matchIDControl.invalid || busy()">
        Submit
      </button>
    </form>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportMatchFormComponent {
  /** The match ID typed in: the page's, so it can tell which report is waiting for its place in the series. */
  readonly matchID = model('');
  /** The page is sending a report or an action: nothing is sent from here meanwhile. */
  readonly busy = input(false);
  /** Report this match ID (a GUID). */
  readonly submitted = output<string>();

  /** The match ID typed in, valid only as a GUID (bare or wrapped in balanced braces). GUID_PATTERN is fully anchored
   * (^...$), so one RegExp kept for the control's life is fine: no 'g' flag, so no lastIndex to go stale between checks. */
  protected readonly matchIDControl = new FormControl(this.matchID(), {
    nonNullable: true,
    validators: regexMatchValidValidator(new RegExp(GUID_PATTERN), GUID_ERROR_MESSAGE),
  });

  constructor() {
    this.matchIDControl.valueChanges.subscribe(value => this.matchID.set(value));
  }

  submit(): void {
    if (this.matchIDControl.valid)
      this.submitted.emit(this.matchID());
  }
}
