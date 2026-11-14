import { Component, OnInit, ChangeDetectionStrategy, inject, linkedSignal } from '@angular/core';
import { Subject } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { DndModule, DndDropEvent } from 'ngx-drag-drop';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import type { TeamStandingDto } from '@api/models';
import { SeedOrderingDialogStore } from './seedOrderingDialog.store';

/** A team's standing, with the seed it is given as the commissioner orders the list (unset until the standings arrive). */
interface TeamStanding extends TeamStandingDto {
  seed?: number;
}

@Component({
    selector: 'app-seed-ordering-dialog',
    imports: [
        MatDialogModule,
        MatButtonModule,
        MatSnackBarModule,
        DndModule,
        MatListModule,
        MatIconModule
    ],
    templateUrl: './seedOrderingDialog.component.html',
    styleUrl: './seedOrderingDialog.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [SeedOrderingDialogStore],
})
export class SeedOrderingDialogComponent implements OnInit {
  private readonly store = inject(SeedOrderingDialogStore);
  data = inject(MAT_DIALOG_DATA);
  private dialogRef = inject<MatDialogRef<SeedOrderingDialogComponent>>(MatDialogRef);

  // A Subject rather than an output(): the request can finish after the dialog was closed
  // (Esc, backdrop, Cancel), and an output() stops delivering once the dialog is destroyed.
  readonly seedingOrder = new Subject<void>();
  /** The teams in the order the commissioner gives them, seeded from the standings as they arrive. */
  readonly teams = linkedSignal<TeamStanding[]>(() => this.store.standings().map((team, index) => ({ ...team, seed: index + 1 })));
  readonly loading = this.store.loading;
  private readonly seasonID: number = this.data;

  ngOnInit(): void {
    this.loadTeamStandings();
  }

  loadTeamStandings(): void {
    this.store.load(this.seasonID);
  }

  onDrop(event: DndDropEvent): void {
    if (event.dropEffect === 'move') {
      const draggedTeam = event.data as TeamStanding;
      const teams = [...this.teams()];
      const draggedIndex = teams.findIndex(t => t.teamID === draggedTeam.teamID);
      let newIndex = event.index;
      
      if (draggedIndex !== -1 && typeof newIndex !== 'undefined') {
        // Remove from old position
        teams.splice(draggedIndex, 1);
        
        // Adjust index if we're moving down in the same list
        if (draggedIndex < newIndex) {
          newIndex--;
        }
        
        // Insert at new position
        teams.splice(newIndex, 0, draggedTeam);
        
        // Update seed numbers
        this.teams.set(teams.map((team, index) => ({ ...team, seed: index + 1 })));
      }
    }
  }

  /** Moves the team at `index` one place up (-1) or down (1), as the buttons beside it do. */
  move(index: number, delta: -1 | 1): void {
    const teams = [...this.teams()];
    const to = index + delta;
    if (to < 0 || to >= teams.length)
      return;
    [teams[index], teams[to]] = [teams[to], teams[index]];
    this.teams.set(teams.map((team, i) => ({ ...team, seed: i + 1 })));
  }

  onSubmit(): void {
    const customSeeding = this.teams().map(team => ({
      teamID: team.teamID,
      seed: team.seed!
    }));

    this.store.setSeeds(this.seasonID, customSeeding, () => {
      this.seedingOrder.next();
      this.seedingOrder.complete();
      this.dialogRef.close(true);
    });
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}