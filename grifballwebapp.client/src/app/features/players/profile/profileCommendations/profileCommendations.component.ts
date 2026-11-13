import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ProfileService } from '@api/services/profile.service';
import type { CommendationDto, ProfileCommendationsDto } from '@api/models';

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
})
export class ProfileCommendationsComponent {
  private api = inject(ProfileService);

  readonly userID = input.required<number>();

  readonly commendations = rxResource({
    params: () => this.userID(),
    stream: ({ params }) => this.api.getCommendations({ userID: params }),
  });

  readonly data = computed<ProfileCommendationsDto | null>(() => this.commendations.hasValue() ? this.commendations.value() : null);
  readonly failed = computed(() => this.commendations.status() === 'error');

  readonly progress = tierProgress;
}
