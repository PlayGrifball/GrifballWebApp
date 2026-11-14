import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CreateBracketDialogStore } from './createBracketDialog.store';

@Component({
    selector: 'app-create-bracket-dialog',
    imports: [
        MatDialogModule,
        MatButtonModule,
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ErrorMessageComponent,
        MatCheckboxModule,
        MatSnackBarModule
    ],
    templateUrl: './createBracketDialog.component.html',
    styleUrl: './createBracketDialog.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [CreateBracketDialogStore],
})
export class CreateBracketDialogComponent {
  private readonly store = inject(CreateBracketDialogStore);
  data = inject(MAT_DIALOG_DATA);
  private dialogRef = inject(MatDialogRef);

  readonly seasonID: number = this.data;

  participantsCount: number | null = 8;
  doubleElimination: boolean = true;
  bestOf: number | null = 3;

  // A Subject rather than an output(): the request can finish after the dialog was closed
  // (Esc, backdrop, Cancel), and an output() stops delivering once the dialog is destroyed.
  // It is not completed: a second submit (a double-click) can still answer after the first.
  readonly bracketCreated = new Subject<void>();

  onSubmit() {
    const options = { participantsCount: this.participantsCount!, doubleElimination: this.doubleElimination, bestOf: this.bestOf! };
    this.store.create(this.seasonID, options, () => {
      this.bracketCreated.next();
      this.dialogRef.close(true);
    });
  }
}
