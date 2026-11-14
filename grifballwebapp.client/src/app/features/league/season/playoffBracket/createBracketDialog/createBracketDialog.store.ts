import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BracketsService } from '@api/services/brackets.service';

/** What a new bracket is: how many teams, double elimination or not, and how many games a match is. */
export interface BracketOptions {
  participantsCount: number;
  doubleElimination: boolean;
  bestOf: number;
}

/** Making a season's playoff bracket. One per dialog (the component provides it). */
@Injectable()
export class CreateBracketDialogStore {
  private readonly brackets = inject(BracketsService);
  private readonly snackBar = inject(MatSnackBar);

  /** Makes the season's bracket; `created` runs once it is, and a failure is said in a snackbar. */
  create(seasonID: number, options: BracketOptions, created: () => void): void {
    this.brackets.createBracket({ seasonID, ...options }).subscribe({
      next: () => created(),
      error: e => {
        console.log(e);
        this.snackBar.open('Failed to create bracket', 'Close');
      },
    });
  }
}
