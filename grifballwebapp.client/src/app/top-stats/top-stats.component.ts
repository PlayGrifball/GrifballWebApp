import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
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
    changeDetection: ChangeDetectionStrategy.Eager
})
export class TopStatsComponent implements OnInit {
  public kills: KillsDto[] = [];
  public displayedColumns: string[] = ['rank', 'gamertag', 'kills'];

  constructor(private http: ApiClientService, private snackBar: MatSnackBar)
  {
  }

  ngOnInit() {
    this.getKills();
  }

  getKills() {
    this.http.getKills().subscribe({
      next: (result) => this.kills = result,
      error: (error) => console.error(error),
      complete: () => {
        //console.log("Got kills");
        //this.snackBar.open("Got Kills", "Close")
      }
    });

  }
}
