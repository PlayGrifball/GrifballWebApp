import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Inject, Input } from '@angular/core';
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
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        ErrorMessageComponent,
        MatSnackBarModule
    ],
    templateUrl: './createRegularMatchesDialog.component.html',
    styleUrl: './createRegularMatchesDialog.component.scss'
})
export class CreateRegularMatchesDialogComponent {
  @Input({ required: true }) seasonID!: number;

  homeMatchesPerTeam: number | null = 1;
  bestOf: number | null = 1;

  regularMatchesCreated = new EventEmitter();

  constructor(private http: HttpClient, @Inject(MAT_DIALOG_DATA) public data: number, private snackBar: MatSnackBar) {
    this.seasonID = data;
  }

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
