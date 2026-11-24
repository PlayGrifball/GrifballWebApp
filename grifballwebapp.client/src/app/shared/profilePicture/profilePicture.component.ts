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
  /** The name the initial comes from (the gamertag or display name). */
  readonly name = input<string | null | undefined>();
  readonly kind = input<ProfilePictureKind>('gamerPicture');
  /** The accent border the signed-in user's own square has. */
  readonly accent = input(false);

  protected readonly initial = computed(() => this.name()?.trim().charAt(0).toUpperCase() || '?');

  protected readonly src = computed(() => {
    const userID = this.userID();
    if (userID == null) return null;
    return `/api/users/${userID}/${this.kind() === 'haloEmblem' ? 'halo-emblem' : 'gamer-picture'}`;
  });

  /** The image answered 404 or was no image: the initial stays. A new user or kind gets a fresh try. */
  protected readonly failed = linkedSignal<string | null, boolean>({ source: this.src, computation: () => false });
  protected readonly loaded = linkedSignal<string | null, boolean>({ source: this.src, computation: () => false });
}
