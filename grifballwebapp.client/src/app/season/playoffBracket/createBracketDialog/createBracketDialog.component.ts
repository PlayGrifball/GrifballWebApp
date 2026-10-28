import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { ErrorMessageComponent } from '../../../validation/errorMessage.component';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { HttpClient, HttpParams } from '@angular/common/http';
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
    const params = new HttpParams()
      .set('seasonID', this.seasonID)
      .set('participantsCount', this.participantsCount!)
      .set('doubleElimination', this.doubleElimination)
      .set('bestOf', this.bestOf!);
    this.http.get("api/Brackets/CreateBracket", { params })
      .subscribe({
        next: r => {
          this.bracketCreated.next();
          this.dialogRef.close(true);
        },
        error: e => {
          console.log(e);
          this.snackBar.open("Failed to create bracket", "Close");
        },
      })
  }
}
