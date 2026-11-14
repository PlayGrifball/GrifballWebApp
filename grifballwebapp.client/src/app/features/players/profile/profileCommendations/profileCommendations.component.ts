import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import type { CommendationDto, ProfileCommendationsDto } from '@api/models';
import { ProfileCommendationsStore } from './profileCommendations.store';

// Re-exported for this page's own spec, which imports them from here (kept, to not churn an unrelated import).
export type { CommendationDto, ProfileCommendationsDto };

/** The way from the tier reached to the next, in percent; full once the last tier is reached. */
export function tierProgress(c: CommendationDto): number {
  if (c.nextTierAt == null) return 100;
  const span = c.nextTierAt - c.tierFloor;
  if (span <= 0) return 100;
  return Math.max(0, Math.min(100, Math.round((c.count - c.tierFloor) / span * 100)));
}

/** A player's commendations on their profile: their most earned Halo Infinite medals, each with its tier and the way to the next. */
@Component({
  selector: 'app-profile-commendations',
  imports: [DecimalPipe],
  templateUrl: './profileCommendations.component.html',
  styleUrl: './profileCommendations.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ProfileCommendationsStore],
})
export class ProfileCommendationsComponent {
  private readonly store = inject(ProfileCommendationsStore);

  readonly userID = input.required<number>();

  readonly loading = this.store.loading;
  readonly data = this.store.data;
  readonly failed = this.store.failed;

  readonly progress = tierProgress;

  constructor() {
    this.store.userID.use(this.userID);
  }
}
