import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';

/**
 * A user's picture in the square the site used to fill with their initial: the image the site shows for them (their Halo
 * emblem or gamer picture, as they chose, and the other when that one is missing), or the initial itself when the user
 * has neither yet. The size comes from `--profile-picture-size` (38px).
 */
@Component({
  selector: 'app-profile-picture',
  templateUrl: './profilePicture.component.html',
  styleUrl: './profilePicture.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
})
export class ProfilePictureComponent {
  /** The site user whose image to show; none shows only the initial. */
  readonly userID = input<number | null | undefined>();
  /** An Xbox account to show without any site user (a Halo game of people with no account); wins over userID. */
  readonly xboxUserID = input<number | string | null | undefined>();
  /** The name the initial comes from (the gamertag or display name). */
  readonly name = input<string | null | undefined>();
  /** The accent border the signed-in user's own square has. */
  readonly accent = input(false);

  protected readonly initial = computed(() => this.name()?.trim().charAt(0).toUpperCase() || '?');

  protected readonly src = computed(() => {
    const xboxUserID = this.xboxUserID();
    if (xboxUserID != null) return `/api/xbox-accounts/${xboxUserID}/profile-picture`;
    const userID = this.userID();
    return userID == null ? null : `/api/users/${userID}/profile-picture`;
  });

  /** Whether the image is still coming, is in, or answered 404 (the initial stays). A new user gets a fresh try. */
  protected readonly state = linkedSignal<string | null, 'pending' | 'loaded' | 'failed'>({ source: this.src, computation: () => 'pending' });
}
