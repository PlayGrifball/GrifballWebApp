import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Inject, Input } from '@angular/core';
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
  standalone: true,
  imports: [
    CommonModule,
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
})
export class CreateBracketDialogComponent {
  @Input({ required: true }) seasonID!: number;

  participantsCount: number | null = 8;
  doubleElimination: boolean = true;
  bestOf: number | null = 3;

  bracketCreated = new EventEmitter();

  constructor(private http: HttpClient, @Inject(MAT_DIALOG_DATA) public data: number, private snackBar: MatSnackBar) {
    this.seasonID = data;
  }

  onSubmit() {
    this.http.get("api/Brackets/CreateBracket?seasonID=" + this.seasonID + "&participantsCount=" + this.participantsCount + "&doubleElimination=" + this.doubleElimination + "&bestOf=" + this.bestOf)
      .subscribe({
        next: r => this.bracketCreated.emit(),
        error: e => {
          console.log(e);
          this.snackBar.open("Failed to create bracket", "Close");
        },
      })
  }
}
