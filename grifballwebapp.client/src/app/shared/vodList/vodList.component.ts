import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { isVideoID, vodDuration, type VodScope } from './vods';
import { VodListStore } from './vodList.store';
import type { VodDto } from '@api/models';

/** The privacy-enhanced player: no YouTube cookies until the video plays. */
export const EMBED_ORIGIN = 'https://www.youtube-nocookie.com';

/**
 * A page's VODs (a season match, season, team, or a player's teams), newest first; nothing at all when there are none.
 * Each is a card that plays in place (youtube-nocookie.com, loaded only on Play so a page of VODs stays light) and links
 * out to YouTube, with what it is of: its season, match and teams, except the page's own.
 */
@Component({
  selector: 'app-vod-list',
  imports: [DatePipe, RouterLink],
  templateUrl: './vodList.component.html',
  styleUrl: './vodList.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [VodListStore],
})
export class VodListComponent {
  private readonly store = inject(VodListStore);
  private sanitizer = inject(DomSanitizer);

  readonly scope = input.required<VodScope>();
  readonly id = input.required<number>();

  readonly duration = vodDuration;

  readonly vods = this.store.vods;
  readonly headingID = computed(() => `vods-${this.scope()}-${this.id()}`);

  /** The VOD playing in its card, and its player's URL. */
  readonly playing = signal<VodDto | null>(null);
  readonly embedUrl = computed<SafeResourceUrl | null>(() => {
    const vod = this.playing();
    // Only a video ID goes in the URL trusted as a frame source
    return vod && isVideoID(vod.youTubeVideoID)
      // eslint-disable-next-line sonarjs/no-angular-bypass-sanitization -- a fixed origin and path, and an ID isVideoID checked
      ? this.sanitizer.bypassSecurityTrustResourceUrl(`${EMBED_ORIGIN}/embed/${vod.youTubeVideoID}?autoplay=1&rel=0`) // NOSONAR(S6268): a fixed origin and path, and an ID isVideoID checked
      : null;
  });

  constructor() {
    this.store.scope.use(this.scope);
    this.store.id.use(this.id);
  }

  play(vod: VodDto): void {
    this.playing.set(vod);
  }

  watchUrl(vod: VodDto): string {
    return 'https://www.youtube.com/watch?v=' + encodeURIComponent(vod.youTubeVideoID);
  }

  canPlay(vod: VodDto): boolean {
    return isVideoID(vod.youTubeVideoID);
  }
}
