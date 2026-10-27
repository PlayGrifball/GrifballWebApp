import { CommonModule } from '@angular/common';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { ApiClientService } from '../api/apiClient.service';
import { SeasonDto } from '../api/dtos/seasonDto';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-season-manager',
    imports: [
        CommonModule,
        MatTableModule,
        RouterModule,
        MatButtonModule
    ],
    templateUrl: './seasonManager.component.html',
    styleUrl: './seasonManager.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonManagerComponent implements OnInit {
  private http = inject(ApiClientService);

  readonly seasons = signal<SeasonDto[]>([]);
  public displayedColumns: string[] = ['seasonName', 'signupsOpen', 'signupsClose', 'draftStart', 'seasonStart', 'seasonEnd', 'signupsCount', 'edit'];

  ngOnInit(): void {
    this.getSeasons();
  }

  getSeasons(): void {
    this.http.getSeasons().subscribe({
      next: (result) => this.seasons.set(result),
      error: (error) => console.error(error),
    });
  }
}
