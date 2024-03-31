import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-season',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule
  ],
  templateUrl: './season.component.html',
  styleUrl: './season.component.scss',
})
export class SeasonComponent implements OnInit {
  private seasonID : number = 0;
  seasonName : string | null = "Season";

  constructor(private route: ActivatedRoute, private api: ApiClientService, private router: Router) {}
  
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
          next: (result) => this.seasonName = result,
        });
  }

}
