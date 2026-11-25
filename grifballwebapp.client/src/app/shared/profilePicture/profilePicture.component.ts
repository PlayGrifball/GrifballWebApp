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
  host: { 'aria-hidden': 'true' },
})
export class ProfilePictureComponent {
  /** The site user whose image to show; none shows only the initial. */
  readonly userID = input<number | null | undefined>();
  /** An Xbox account to show without any site user (a Halo game of people with no account); wins over userID. */
  readonly xboxUserID = input<number | string | null | undefined>();
  /** The name the initial comes from (the gamertag or display name). */
  readonly name = input<string | null | undefined>();
  readonly kind = input<ProfilePictureKind>('gamerPicture');
  /** For an emblem: show the gamer picture when the account has no emblem image (a special emblem Halo gives no picture for). */
  readonly fallbackToGamerPicture = input(false);
  /** The accent border the signed-in user's own square has. */
  readonly accent = input(false);

  protected readonly initial = computed(() => this.name()?.trim().charAt(0).toUpperCase() || '?');

  private readonly account = computed(() => {
    const xboxUserID = this.xboxUserID();
    if (xboxUserID != null) return `/api/xbox-accounts/${xboxUserID}`;
    const userID = this.userID();
    return userID == null ? null : `/api/users/${userID}`;
  });

  /** Whether the emblem was missing and the gamer picture stands in. A new account or kind starts again with the emblem. */
  protected readonly fellBack = linkedSignal<string, boolean>({ source: () => `${this.account()}|${this.kind()}`, computation: () => false });

  protected readonly emblem = computed(() => this.kind() === 'haloEmblem' && !this.fellBack());

  protected readonly src = computed(() => {
    const account = this.account();
    return account == null ? null : `${account}/${this.emblem() ? 'halo-emblem' : 'gamer-picture'}`;
  });

  /** Whether the image is still coming, is in, or answered 404 (the initial stays). A new user or kind gets a fresh try. */
  protected readonly state = linkedSignal<string | null, 'pending' | 'loaded' | 'failed'>({ source: this.src, computation: () => 'pending' });

  protected failed(): void {
    if (this.emblem() && this.fallbackToGamerPicture())
      this.fellBack.set(true);
    else
      this.state.set('failed');
  }
}
