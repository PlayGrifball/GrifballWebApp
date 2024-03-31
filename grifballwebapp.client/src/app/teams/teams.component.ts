import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { TeamResponseDto } from '../api/dtos/teamResponseDto';
import { PlayerDto } from '../api/dtos/playerDto';

@Component({
  selector: 'app-teams',
  standalone: true,
  imports: [
    CommonModule,
  ],
  templateUrl: './teams.component.html',
  styleUrl: './teams.component.scss',
})
export class TeamsComponent {
  private seasonID : number = 0;
  teams! : TeamResponseDto[];
  playerPool! : PlayerDto[];

  constructor(private route: ActivatedRoute, private api: ApiClientService) {}
  
  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0) {
      this.api.getCurrentSeasonID()
        .subscribe({
          next: (result) => {
            this.seasonID = result;
            this.load();
          },
        });
    }
    else {
      this.load();
    }
  }

  load(): void {
    this.api.getTeams(this.seasonID)
        .subscribe({
          next: (result) => this.teams = result,
        });
    this.api.getPlayerPool(this.seasonID)
        .subscribe({
          next: (result) => this.playerPool = result,
        });
  }
}
