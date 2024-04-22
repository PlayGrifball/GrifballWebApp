import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { ApiClientService } from '../api/apiClient.service';
import { SeasonDto } from '../api/dtos/seasonDto';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-season-manager',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    RouterModule,
    MatButtonModule
  ],
  templateUrl: './seasonManager.component.html',
  styleUrl: './seasonManager.component.scss'
})
export class SeasonManagerComponent implements OnInit {
  public seasons: SeasonDto[] = [];
  public displayedColumns: string[] = ['seasonName', 'signupsOpen', 'signupsClose', 'draftStart', 'seasonStart', 'seasonEnd', 'signupsCount', 'edit'];

  constructor(private http: ApiClientService) {
  }

  ngOnInit(): void {
    this.getSeasons();
  }

  getSeasons(): void {
    this.http.getSeasons().subscribe({
      next: (result) => this.seasons = result,
      error: (error) => console.error(error),
    });
  }
}
