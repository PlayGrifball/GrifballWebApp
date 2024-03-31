import { Component, OnInit } from '@angular/core';
import { KillsDto } from '../api/dtos/killsDto';
import { HttpClientModule } from '@angular/common/http';
import { MatTableModule } from '@angular/material/table';
import { CommonModule } from '@angular/common';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-top-stats',
  standalone: true,
  imports: [
    CommonModule,
    HttpClientModule,
    MatTableModule,
    MatSnackBarModule
  ],
  templateUrl: './top-stats.component.html',
  styleUrl: './top-stats.component.css'
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
