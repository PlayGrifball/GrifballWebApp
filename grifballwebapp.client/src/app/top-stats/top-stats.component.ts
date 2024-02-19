import { Component, OnInit } from '@angular/core';
import { KillsDto } from './killsDto';
import { HttpClientModule, HttpClient } from '@angular/common/http';
import { MatTableModule } from '@angular/material/table';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-top-stats',
  standalone: true,
  imports: [
    CommonModule,
    HttpClientModule,
    MatTableModule
  ],
  templateUrl: './top-stats.component.html',
  styleUrl: './top-stats.component.css'
})
export class TopStatsComponent implements OnInit {
  public kills: KillsDto[] = [];
  public displayedColumns: string[] = ['rank', 'gamertag', 'kills'];

  constructor(private http: HttpClient)
  {
    console.log("Constructed top stats");
  }

  ngOnInit() {
    this.getKills();
  }

  getKills() {
    this.http.get<KillsDto[]>('/api/stats/TopKills').subscribe(
      (result) => {
        this.kills = result;
      },
      (error) => {
        console.error(error);
      }
    );
  }
}
