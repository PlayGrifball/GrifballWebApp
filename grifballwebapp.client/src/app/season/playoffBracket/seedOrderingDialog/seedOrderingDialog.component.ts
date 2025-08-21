import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Inject, OnInit } from '@angular/core';
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
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatSnackBarModule,
        DndModule,
        MatListModule,
        MatIconModule
    ],
    templateUrl: './seedOrderingDialog.component.html',
    styleUrl: './seedOrderingDialog.component.scss'
})
export class SeedOrderingDialogComponent implements OnInit {
  @Output() seedingOrder = new EventEmitter<void>();
  teams: TeamStanding[] = [];
  loading = true;
  private seasonID: number;

  constructor(
    private http: HttpClient, 
    @Inject(MAT_DIALOG_DATA) public data: number, 
    private snackBar: MatSnackBar,
    private dialogRef: MatDialogRef<SeedOrderingDialogComponent>
  ) {
    this.seasonID = data;
  }

  ngOnInit(): void {
    this.loadTeamStandings();
  }

  loadTeamStandings(): void {
    this.http.get<TeamStanding[]>(`api/TeamStandings/GetTeamStandings/${this.seasonID}`)
      .subscribe({
        next: (standings) => {
          this.teams = standings.map((team, index) => ({
            ...team,
            seed: index + 1
          }));
          this.loading = false;
        },
        error: (e) => {
          console.log(e);
          this.snackBar.open("Failed to load team standings", "Close");
          this.loading = false;
        }
      });
  }

  onDrop(event: DndDropEvent): void {
    if (event.dropEffect === 'move') {
      const draggedTeam = event.data as TeamStanding;
      const draggedIndex = this.teams.findIndex(t => t.teamID === draggedTeam.teamID);
      let newIndex = event.index;
      
      if (draggedIndex !== -1 && typeof newIndex !== 'undefined') {
        // Remove from old position
        this.teams.splice(draggedIndex, 1);
        
        // Adjust index if we're moving down in the same list
        if (draggedIndex < newIndex) {
          newIndex--;
        }
        
        // Insert at new position
        this.teams.splice(newIndex, 0, draggedTeam);
        
        // Update seed numbers
        this.teams.forEach((team, index) => {
          team.seed = index + 1;
        });
      }
    }
  }

  onSubmit(): void {
    const customSeeding = this.teams.map(team => ({
      teamID: team.teamID,
      seed: team.seed!
    }));

    this.http.post(`api/Brackets/SetCustomSeeds/${this.seasonID}`, customSeeding)
      .subscribe({
        next: () => {
          this.seedingOrder.emit();
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