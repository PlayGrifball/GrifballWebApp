import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';

/** Which of the user's stored Xbox images to show. */
export type ProfilePictureKind = 'gamerPicture' | 'haloEmblem';

/**
 * A user's picture in the square the site used to fill with their initial: their stored gamer picture (or Halo
 * emblem), or the initial itself when the user has no image yet. The size comes from `--profile-picture-size` (38px).
 */
@Component({
  selector: 'app-profile-picture',
  templateUrl: './profilePicture.component.html',
  styleUrl: './profilePicture.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePictureComponent {
  /** The site user whose image to show; none shows only the initial. */
  readonly userID = input<number | null | undefined>();
  /** An Xbox account to show without any site user (a Halo game of people with no account); wins over userID. */
  readonly xboxUserID = input<number | string | null | undefined>();
  /** The name the initial comes from (the gamertag or display name). */
  readonly name = input<string | null | undefined>();
  readonly kind = input<ProfilePictureKind>('gamerPicture');
  /** The accent border the signed-in user's own square has. */
  readonly accent = input(false);

  protected readonly initial = computed(() => this.name()?.trim().charAt(0).toUpperCase() || '?');

  protected readonly src = computed(() => {
    const file = this.kind() === 'haloEmblem' ? 'halo-emblem' : 'gamer-picture';
    const xboxUserID = this.xboxUserID();
    if (xboxUserID != null) return `/api/xbox-accounts/${xboxUserID}/${file}`;
    const userID = this.userID();
    return userID == null ? null : `/api/users/${userID}/${file}`;
  });

  /** Whether the image is still coming, is in, or answered 404 (the initial stays). A new user or kind gets a fresh try. */
  protected readonly state = linkedSignal<string | null, 'pending' | 'loaded' | 'failed'>({ source: this.src, computation: () => 'pending' });
}
