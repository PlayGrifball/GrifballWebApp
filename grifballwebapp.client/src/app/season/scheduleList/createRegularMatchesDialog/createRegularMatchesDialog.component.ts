import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { ErrorMessageComponent } from '../../../validation/errorMessage.component';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { HttpClient } from '@angular/common/http';

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
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateRegularMatchesDialogComponent {
  private http = inject(HttpClient);
  data = inject(MAT_DIALOG_DATA);
  private snackBar = inject(MatSnackBar);

  readonly seasonID: number = this.data;

  homeMatchesPerTeam: number | null = 1;
  bestOf: number | null = 1;

  // A Subject rather than an output(): the request can finish after the dialog was closed
  // (Esc, backdrop, Cancel), and an output() stops delivering once the dialog is destroyed.
  // It is not completed: the dialog stays open, and each submit (re)creates on the server.
  readonly regularMatchesCreated = new Subject<void>();

  onSubmit() {
    this.http.get("api/MatchPlanner/CreateSeasonMatches?seasonID=" + this.seasonID + "&homeMatchesPerTeam=" + this.homeMatchesPerTeam + "&bestOf=" + this.bestOf)
      .subscribe({
        next: r => this.regularMatchesCreated.next(),
        error: e => {
          console.log(e);
          this.snackBar.open("Failed to create regular matches", "Close");
        },
      })
  }
}
