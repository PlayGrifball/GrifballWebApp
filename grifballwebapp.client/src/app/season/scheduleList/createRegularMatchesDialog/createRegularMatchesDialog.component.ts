import { Component, ChangeDetectionStrategy, inject, output } from '@angular/core';
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

  readonly regularMatchesCreated = output();

  onSubmit() {
    this.http.get("api/MatchPlanner/CreateSeasonMatches?seasonID=" + this.seasonID + "&homeMatchesPerTeam=" + this.homeMatchesPerTeam + "&bestOf=" + this.bestOf)
      .subscribe({
        next: r => this.regularMatchesCreated.emit(),
        error: e => {
          console.log(e);
          this.snackBar.open("Failed to create regular matches", "Close");
        },
      })
  }
}
