import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';

export interface CommendationDto {
  medalID: number;
  medalName: string;
  description?: string | null;
  difficultyName?: string | null;
  typeName?: string | null;
  /** Times earned. */
  count: number;
  /** Games it was earned in. */
  matches: number;
  /** Tiers reached; 0 before the first. */
  tier: number;
  /** The count of the tier reached; 0 before the first. */
  tierFloor: number;
  /** The count of the next tier; null once the last is reached. */
  nextTierAt?: number | null;
}

export interface ProfileCommendationsDto {
  userID: number;
  totalMedals: number;
  distinctMedals: number;
  commendations: CommendationDto[];
}

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
  private http = inject(HttpClient);

  readonly userID = input.required<number>();

  readonly commendations = rxResource({
    params: () => this.userID(),
    stream: ({ params }) => this.http.get<ProfileCommendationsDto>('api/profile/getcommendations/' + params),
  });

  readonly data = computed<ProfileCommendationsDto | null>(() => this.commendations.hasValue() ? this.commendations.value() : null);
  readonly failed = computed(() => this.commendations.status() === 'error');

  readonly progress = tierProgress;
}
