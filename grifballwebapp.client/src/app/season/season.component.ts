import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatButtonModule } from '@angular/material/button';
import { ScheduleListComponent } from "./scheduleList/scheduleList.component";
import { PlayoffBracketComponent } from './playoffBracket/playoffBracket.component';
import { TeamStandingsComponent } from './teamStandings/teamStandings.component';

@Component({
    selector: 'app-season',
    templateUrl: './season.component.html',
    styleUrl: './season.component.scss',
    imports: [
        RouterModule,
        MatButtonModule,
        ScheduleListComponent,
        PlayoffBracketComponent,
        TeamStandingsComponent
    ],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);
  private router = inject(Router);

  private seasonID : number = 0;
  readonly seasonName = signal<string | null>("Season");
  
  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));
    
    if (this.seasonID === 0) {
      this.api.getCurrentSeasonID()
        .subscribe({
          next: (result) => {
            this.seasonID = result;
            this.getSeasonName();
          },
        });
    }
    else {
      this.getSeasonName();
    }
  }

  getSeasonName(): void {
    this.api.getSeasonName(this.seasonID)
        .subscribe({
          next: (result) => this.seasonName.set(result),
        });
  }

}
