import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { KillsDto } from '../api/dtos/killsDto';
import { MatTableModule } from '@angular/material/table';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
    selector: 'app-top-stats',
    imports: [
        MatTableModule,
        MatSnackBarModule
    ],
    templateUrl: './top-stats.component.html',
    styleUrl: './top-stats.component.css',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TopStatsComponent implements OnInit {
  private http = inject(ApiClientService);
  private snackBar = inject(MatSnackBar);

  readonly kills = signal<KillsDto[]>([]);
  public displayedColumns: string[] = ['rank', 'gamertag', 'kills'];

  ngOnInit() {
    this.getKills();
  }

  getKills() {
    this.http.getKills().subscribe({
      next: (result) => this.kills.set(result),
      error: (error) => console.error(error),
      complete: () => {
        //console.log("Got kills");
        //this.snackBar.open("Got Kills", "Close")
      }
    });

  }
}
