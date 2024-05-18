import { CommonModule } from '@angular/common';
import { Component, Inject, Input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { ErrorMessageComponent } from '../../../validation/errorMessage.component';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { HttpClient } from '@angular/common/http';

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
    MatCheckboxModule
  ],
  templateUrl: './createBracketDialog.component.html',
  styleUrl: './createBracketDialog.component.scss',
})
export class CreateBracketDialogComponent {
  @Input({ required: true }) seasonID!: number;

  participantsCount: number | null = 8;
  doubleElimination: boolean = true;

  constructor(private http: HttpClient, @Inject(MAT_DIALOG_DATA) public data: number) {
    this.seasonID = data;
  }

  onSubmit() {
    this.http.get("api/Brackets/CreateBracket?seasonID=" + this.seasonID + "&participantsCount=" + this.participantsCount + "&doubleElimination=" + this.doubleElimination)
      .subscribe({
        next: r => console.log(r),
        error: e => console.log(e),
      })
  }
}
