import { CommonModule } from '@angular/common';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { SeasonSettingsApiService } from '../api/seasonSettingsApi.service';
import { SeasonSettings } from '../api/seasonSettings';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

import { ToDatePipe } from '../shared/toDate.pipe';
@Component({
    selector: 'app-season-manager',
    imports: [
        CommonModule,
        MatTableModule,
        RouterModule,
        MatButtonModule,
        ToDatePipe,
    ],
    templateUrl: './seasonManager.component.html',
    styleUrl: './seasonManager.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonManagerComponent implements OnInit {
  private api = inject(SeasonSettingsApiService);

  /** The seasons; undefined while they load. */
  readonly seasons = signal<SeasonSettings[] | undefined>(undefined);
  readonly error = signal(false);
  readonly dateFormat = 'MMM d, y, h:mm a';
  public displayedColumns: string[] = ['seasonName', 'signupsOpen', 'signupsClose', 'draftStart', 'seasonStart', 'seasonEnd', 'signupsCount', 'edit'];

  ngOnInit(): void {
    this.getSeasons();
  }

  getSeasons(): void {
    this.api.getSeasons().subscribe({
      next: (result) => this.seasons.set(result),
      error: (error) => {
        console.error(error);
        this.error.set(true);
      },
    });
  }
}
