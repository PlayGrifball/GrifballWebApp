import { HttpClient } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
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
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private router = inject(Router);

  private seasonID: number = 0;

  readonly teamStandings = signal<TeamStandingDto[]>([]);
  /** The standings have loaded (or failed to, or there is no season). */
  readonly loaded = signal(false);

  displayedColumns: string[] = ['rank', 'teamName', 'wins', 'losses'];

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0) {
      this.loaded.set(true);
      return;
    }

    this.http.get<TeamStandingDto[]>("api/TeamStandings/GetTeamStandings/" + this.seasonID)
      .subscribe(
        {
          next: r => {
            this.teamStandings.set(r);
            this.loaded.set(true);
          },
          error: e => {
            console.log(e);
            this.loaded.set(true);
          }
        });
  }
}
