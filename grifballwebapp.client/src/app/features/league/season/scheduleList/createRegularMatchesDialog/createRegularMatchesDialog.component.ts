import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CreateRegularMatchesDialogStore } from './createRegularMatchesDialog.store';

@Component({
    selector: 'app-create-regular-matches-dialog',
    imports: [
        MatDialogModule,
        MatButtonModule,
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ErrorMessageComponent,
        MatSnackBarModule
    ],
    templateUrl: './createRegularMatchesDialog.component.html',
    styleUrl: './createRegularMatchesDialog.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [CreateRegularMatchesDialogStore],
})
export class CreateRegularMatchesDialogComponent {
  private readonly store = inject(CreateRegularMatchesDialogStore);
  data = inject(MAT_DIALOG_DATA);
  private dialogRef = inject(MatDialogRef);

  readonly seasonID: number = this.data;

  homeMatchesPerTeam: number | null = 1;
  bestOf: number | null = 1;

  // A Subject rather than an output(): the request can finish after the dialog was closed
  // (Esc, backdrop, Cancel), and an output() stops delivering once the dialog is destroyed.
  // It is not completed: a second submit (a double-click) can still answer after the first.
  readonly regularMatchesCreated = new Subject<void>();

  onSubmit() {
    this.store.create(this.seasonID, this.homeMatchesPerTeam!, this.bestOf!, () => {
      this.regularMatchesCreated.next();
      this.dialogRef.close(true);
    });
  }
}
