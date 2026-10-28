import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { ErrorMessageComponent } from '../../../validation/errorMessage.component';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { HttpClient } from '@angular/common/http';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

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
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateBracketDialogComponent {
  private http = inject(HttpClient);
  data = inject(MAT_DIALOG_DATA);
  private snackBar = inject(MatSnackBar);

  readonly seasonID: number = this.data;

  participantsCount: number | null = 8;
  doubleElimination: boolean = true;
  bestOf: number | null = 3;

  // A Subject rather than an output(): the request can finish after the dialog was closed
  // (Esc, backdrop, Cancel), and an output() stops delivering once the dialog is destroyed.
  // It is not completed: the dialog stays open, and each submit (re)creates on the server.
  readonly bracketCreated = new Subject<void>();

  onSubmit() {
    this.http.get("api/Brackets/CreateBracket?seasonID=" + this.seasonID + "&participantsCount=" + this.participantsCount + "&doubleElimination=" + this.doubleElimination + "&bestOf=" + this.bestOf)
      .subscribe({
        next: r => this.bracketCreated.next(),
        error: e => {
          console.log(e);
          this.snackBar.open("Failed to create bracket", "Close");
        },
      })
  }
}
