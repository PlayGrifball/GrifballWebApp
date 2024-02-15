import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';

interface KillsDto {
  rank: number;
  gamertag: string;
  kills: number;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  public kills: KillsDto[] = [];

  constructor(private http: HttpClient) {}

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

  title = 'Home';
}
