import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SeasonService } from '@api/services/season.service';

/** What the app shell asks the server for: the current season, for the side bar's link. One per app (the root component provides it). */
@Injectable()
export class AppStore {
  private readonly seasons = inject(SeasonService);
  private readonly destroyRef = inject(DestroyRef);

  /** Finds the current season; `found` gets its ID when there is one (the server answers 0 for none). Without it,
   * the side bar just has no link for it; an answer after the app is gone changes nothing. */
  loadCurrentSeason(found: (seasonID: number) => void): void {
    this.seasons.getCurrentSeasonId().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: seasonID => {
        if (seasonID !== 0)
          found(seasonID);
      },
      error: () => {},
    });
  }
}
