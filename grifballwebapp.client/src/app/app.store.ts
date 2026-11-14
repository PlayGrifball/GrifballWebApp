import { inject, Injectable } from '@angular/core';
import { SeasonService } from '@api/services/season.service';

/** What the app shell asks the server for: the current season, for the side bar's link. One per app (the root component provides it). */
@Injectable()
export class AppStore {
  private readonly seasons = inject(SeasonService);

  /** Finds the current season; `found` gets its ID when there is one (the server answers 0 for none). Without it,
   * the side bar just has no link for it. */
  loadCurrentSeason(found: (seasonID: number) => void): void {
    this.seasons.getCurrentSeasonId().subscribe({
      next: seasonID => {
        if (seasonID !== 0)
          found(seasonID);
      },
      error: () => {},
    });
  }
}
