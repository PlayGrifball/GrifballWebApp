import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { TeamStandingDto } from './teamStandingDto';
import { MatTableModule } from '@angular/material/table';

@Component({
    selector: 'app-team-standings',
    imports: [
        CommonModule,
        MatTableModule,
        RouterModule
    ],
    templateUrl: './teamStandings.component.html',
    styleUrl: './teamStandings.component.scss'
})
export class TeamStandingsComponent implements OnInit {
  private seasonID: number = 0;

  teamStandings: TeamStandingDto[] = [];

  displayedColumns: string[] = ['teamName', 'wins', 'losses'];

  constructor(private route: ActivatedRoute, private http: HttpClient, private router: Router) { }

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0)
      return;

    this.http.get<TeamStandingDto[]>("api/TeamStandings/GetTeamStandings/" + this.seasonID)
      .subscribe(
        {
          next: r => this.teamStandings = r,
          error: e => console.log(e)
        });
  }
}
