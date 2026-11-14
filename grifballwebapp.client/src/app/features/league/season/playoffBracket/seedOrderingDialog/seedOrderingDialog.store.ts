import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { TeamStandingDto } from '@api/models';
import { BracketsService } from '@api/services/brackets.service';
import { TeamStandingsService } from '@api/services/team-standings.service';

/** A team's seed in the bracket. */
export interface CustomSeed {
  teamID: number;
  seed: number;
}

/** The season's standings to seed the bracket from, and setting the seeds. One per dialog (the component provides it). */
@Injectable()
export class SeedOrderingDialogStore {
  private readonly brackets = inject(BracketsService);
  private readonly teamStandings = inject(TeamStandingsService);
  private readonly snackBar = inject(MatSnackBar);

  /** The standings, best first; none until they arrive, or when they could not be loaded. */
  private readonly _standings = signal<TeamStandingDto[]>([]);
  readonly standings = this._standings.asReadonly();
  private readonly _loading = signal(true);
  readonly loading = this._loading.asReadonly();

  load(seasonID: number): void {
    this.teamStandings.getTeamStandings({ seasonID }).subscribe({
      next: standings => {
        this._standings.set(standings);
        this._loading.set(false);
      },
      error: e => {
        console.log(e);
        this.snackBar.open('Failed to load team standings', 'Close');
        this._loading.set(false);
      },
    });
  }

  /** Sets the bracket's seeds; `done` runs once they are, and a failure is said in a snackbar. */
  setSeeds(seasonID: number, seeds: CustomSeed[], done: () => void): void {
    this.brackets.setCustomSeeds({ seasonID, body: seeds }).subscribe({
      next: () => done(),
      error: e => {
        console.log(e);
        this.snackBar.open('Failed to set custom seeds', 'Close');
      },
    });
  }
}
