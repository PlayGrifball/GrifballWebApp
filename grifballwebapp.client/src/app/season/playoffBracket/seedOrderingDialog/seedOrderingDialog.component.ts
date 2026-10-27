import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DndModule, DndDropEvent } from 'ngx-drag-drop';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';

interface TeamStanding {
  teamID: number;
  teamName: string;
  wins: number;
  losses: number;
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
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeedOrderingDialogComponent implements OnInit {
  private http = inject(HttpClient);
  data = inject(MAT_DIALOG_DATA);
  private snackBar = inject(MatSnackBar);
  private dialogRef = inject<MatDialogRef<SeedOrderingDialogComponent>>(MatDialogRef);

  // A Subject rather than an output(): the request can finish after the dialog was closed
  // (Esc, backdrop, Cancel), and an output() stops delivering once the dialog is destroyed.
  readonly seedingOrder = new Subject<void>();
  readonly teams = signal<TeamStanding[]>([]);
  readonly loading = signal(true);
  private readonly seasonID: number = this.data;

  ngOnInit(): void {
    this.loadTeamStandings();
  }

  loadTeamStandings(): void {
    this.http.get<TeamStanding[]>(`api/TeamStandings/GetTeamStandings/${this.seasonID}`)
      .subscribe({
        next: (standings) => {
          this.teams.set(standings.map((team, index) => ({
            ...team,
            seed: index + 1
          })));
          this.loading.set(false);
        },
        error: (e) => {
          console.log(e);
          this.snackBar.open("Failed to load team standings", "Close");
          this.loading.set(false);
        }
      });
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

  onSubmit(): void {
    const customSeeding = this.teams().map(team => ({
      teamID: team.teamID,
      seed: team.seed!
    }));

    this.http.post(`api/Brackets/SetCustomSeeds/${this.seasonID}`, customSeeding)
      .subscribe({
        next: () => {
          this.seedingOrder.next();
          this.seedingOrder.complete();
          this.dialogRef.close(true);
        },
        error: (e) => {
          console.log(e);
          this.snackBar.open("Failed to set custom seeds", "Close");
        }
      });
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}