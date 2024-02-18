import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { KillsDto } from './killsDto';

@Component({
  selector: 'app-top-stats',
  templateUrl: './top-stats.component.html',
  styleUrl: './top-stats.component.css'
})
export class TopStatsComponent implements OnInit {
  public kills: KillsDto[] = [];

  constructor(private http: HttpClient) { }

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
