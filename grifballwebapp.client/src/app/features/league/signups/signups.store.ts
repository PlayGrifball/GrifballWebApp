import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import type { SeasonSummaryDto, SignupResponseDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { SignupsService } from '@api/services/signups.service';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import { SeasonSummaryService } from '../season/seasonSummary';

/**
 * A season's signups and summary: the route's season, or the current one when it names none (0). A new season drops
 * what was shown, and any answer still on its way for the last one. One per page (the component provides it).
 */
@Injectable()
export class SignupsStore {
  private readonly seasons = inject(SeasonService);
  private readonly signups = inject(SignupsService);
  private readonly summaries = inject(SeasonSummaryService);

  /** The route's season (0: the current season): the component's input. */
  readonly seasonID = new StoreInput<number>();

  private readonly _signUps = signal<SignupResponseDto[]>([]);
  readonly signUps = this._signUps.asReadonly();
  private readonly _summary = signal<SeasonSummaryDto | null>(null);
  readonly summary = this._summary.asReadonly();
  /** The season shown: the route's, or the current season's ID once known. */
  private readonly _shownSeasonID = signal(0);
  readonly shownSeasonID = this._shownSeasonID.asReadonly();

  constructor() {
    effect(() => {
      const routeSeasonID = this.seasonID.value();
      if (routeSeasonID === undefined)
        return;
      untracked(() => this.show(routeSeasonID));
    });
  }

  private show(routeSeasonID: number): void {
    this._signUps.set([]);
    this._summary.set(null);
    this._shownSeasonID.set(routeSeasonID);
    if (routeSeasonID !== 0) {
      this.load();
      return;
    }
    this.seasons.getCurrentSeasonId()
      .pipe(whileCurrent(routeSeasonID, this.seasonID.value))
      .subscribe({
        next: seasonID => {
          this._shownSeasonID.set(seasonID);
          this.load();
        },
      });
  }

  private load(): void {
    const seasonID = this.shownSeasonID();
    this.summaries.get(seasonID).pipe(whileCurrent(seasonID, this.shownSeasonID)).subscribe({ next: s => this._summary.set(s), error: () => {} });
    this.signups.getSignups({ seasonID }).pipe(whileCurrent(seasonID, this.shownSeasonID)).subscribe({
      next: result => this._signUps.set(result),
      error: error => console.error(error),
    });
  }
}
