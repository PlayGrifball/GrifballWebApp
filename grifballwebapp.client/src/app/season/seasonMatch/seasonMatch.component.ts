import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { SeasonMatchPageDto } from './seasonMatchPageDto';
import { MatGridListModule } from '@angular/material/grid-list';

@Component({
  selector: 'app-season-match',
  standalone: true,
  imports: [
    CommonModule,
    MatGridListModule,
    RouterModule
  ],
  templateUrl: './seasonMatch.component.html',
  styleUrl: './seasonMatch.component.scss'
})
export class SeasonMatchComponent implements OnInit {
  private seasonMatchID: number = 0;
  seasonMatch: SeasonMatchPageDto | null = null;

  constructor(private route: ActivatedRoute, private http: HttpClient) {
  }

  ngOnInit(): void {
    this.seasonMatchID = Number(this.route.snapshot.paramMap.get('seasonMatchID'));

    this.getPageDto();
  }

  getPageDto(): void {
    this.http.get<SeasonMatchPageDto>('/api/SeasonMatch/GetSeasonMatchPage/' + this.seasonMatchID)
      .subscribe({
        next: result => this.seasonMatch = result,
      });
  }
}
