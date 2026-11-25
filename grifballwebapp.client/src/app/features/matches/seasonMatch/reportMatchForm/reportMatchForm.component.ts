import { ChangeDetectionStrategy, Component, input, model, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { RegexMatchValidValidatorDirective } from '@shared/validation/directives/regexMatchValidValidatorDirective.directive';
import { GUID_ERROR_MESSAGE, GUID_PATTERN } from '@shared/validation/guid';

/**
 * A commissioner reports a game of the match by its Waypoint match ID. Its own component, so its code (a form field,
 * forms, validation) is loaded only for commissioners (the match page defers it), not for every visitor of the page.
 */
@Component({
  selector: 'app-report-match-form',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, ErrorMessageComponent, RegexMatchValidValidatorDirective],
  template: `
    <form class="app-form" #reportMatchForm="ngForm" (ngSubmit)="submitted.emit(matchID())">
      <mat-form-field appearance="outline" class="app-full-width">
        <mat-label>Match ID</mat-label>
        <input matInput required [(ngModel)]="matchID" name="MatchID" [regexExpression]="regex" [errorMessage]="guidErrorMessage">
        <app-error-message [control]="reportMatchForm.controls['MatchID']" />
      </mat-form-field>
      <button mat-flat-button [disabled]="!reportMatchForm.form.valid || busy()">
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

  protected readonly regex = GUID_PATTERN;
  protected readonly guidErrorMessage = GUID_ERROR_MESSAGE;
}
