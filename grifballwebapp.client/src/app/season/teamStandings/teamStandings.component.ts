import { HttpClient } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { TeamStandingDto } from './teamStandingDto';
import { MatTableModule } from '@angular/material/table';

@Component({
    selector: 'app-team-standings',
    imports: [
        MatTableModule,
        RouterModule
    ],
    templateUrl: './teamStandings.component.html',
    styleUrl: './teamStandings.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeamStandingsComponent implements OnInit {
  private seasonID: number = 0;

  teamStandings = signal<TeamStandingDto[]>([]);

  displayedColumns: string[] = ['teamName', 'wins', 'losses'];

  constructor(private route: ActivatedRoute, private http: HttpClient, private router: Router) { }

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0)
      return;

    this.http.get<TeamStandingDto[]>("api/TeamStandings/GetTeamStandings/" + this.seasonID)
      .subscribe(
        {
          next: r => this.teamStandings.set(r),
          error: e => console.log(e)
        });
  }
}
