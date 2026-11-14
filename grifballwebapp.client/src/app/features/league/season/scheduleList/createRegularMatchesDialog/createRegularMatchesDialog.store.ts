import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatchPlannerService } from '@api/services/match-planner.service';

/** Making a season's regular matches. One per dialog (the component provides it). */
@Injectable()
export class CreateRegularMatchesDialogStore {
  private readonly matchPlanner = inject(MatchPlannerService);
  private readonly snackBar = inject(MatSnackBar);

  /** Makes the season's regular matches; `created` runs once they are, and a failure is said in a snackbar. */
  create(seasonID: number, homeMatchesPerTeam: number, bestOf: number, created: () => void): void {
    this.matchPlanner.createSeasonMatches({ seasonID, homeMatchesPerTeam, bestOf }).subscribe({
      next: () => created(),
      error: e => {
        console.log(e);
        this.snackBar.open('Failed to create regular matches', 'Close');
      },
    });
  }
}
