import { CommonModule } from '@angular/common';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { switchMap } from 'rxjs';
import { EventOrganizerService } from '@api/services';
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
  private api = inject(EventOrganizerService);

  /** The seasons; undefined while they load. */
  readonly seasons = signal<SeasonSettings[] | undefined>(undefined);
  readonly error = signal(false);
  readonly dateFormat = 'MMM d, y, h:mm a';
  public displayedColumns: string[] = ['seasonName', 'signupsOpen', 'signupsClose', 'draftStart', 'seasonStart', 'seasonEnd', 'signupsCount', 'edit'];

  ngOnInit(): void {
    this.getSeasons();
  }

  getSeasons(): void {
    // A list never holds nulls (List<SeasonDto> on the server); Luxon is only loaded with the
    // pages that need it (season editing), not with the app: the parser is imported when the seasons arrive.
    this.api.getSeasons().pipe(switchMap(async seasons => {
      const { parseSeason } = await import('../api/seasonSettings');
      return seasons.map(season => parseSeason(season)!);
    })).subscribe({
      next: (result) => this.seasons.set(result),
      error: (error) => {
        console.error(error);
        this.error.set(true);
      },
    });
  }
}
