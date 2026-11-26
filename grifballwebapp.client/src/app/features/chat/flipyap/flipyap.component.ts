import { isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, PLATFORM_ID, afterNextRender, computed, effect, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { FlipyapChatComponent } from './flipyapChat.component';
import { FRAMES, SPRITE_SIZE, type FlipyapFrame } from './flipyapSprite';

export const BLINK_EVERY_MS = 4000;
export const BLINK_FOR_MS = 160;
/** How long his mouth stays open or shut while he answers. */
export const TALK_BEAT_MS = 180;
export const GREETING_FOR_MS = 8000;
/** Where the tab remembers that Flipyap has said hello: once per tab is enough. */
export const GREETED_STORAGE_KEY = 'flipyap-greeted';

/**
 * Flipyap, an Unggoy in pixel art who sits in the bottom right corner of the site and answers questions about the league:
 * the pet opens the chat. He blinks now and then and talks while an answer comes in (still, for someone who asks for less
 * motion). The chat page has the same chat in full, so he stays off it. The chat is made when he is first opened and kept
 * after that, so an answer goes on streaming with it closed.
 */
@Component({
  selector: 'app-flipyap',
  templateUrl: 'flipyap.component.html',
  styleUrl: 'flipyap.component.scss',
  imports: [FlipyapChatComponent],
  host: { '(keydown.escape)': 'close()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FlipyapComponent {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly pet = viewChild<ElementRef<HTMLButtonElement>>('pet');
  private readonly chat = viewChild(FlipyapChatComponent);

  private readonly url = toSignal(
    this.router.events.pipe(filter(event => event instanceof NavigationEnd), map(event => event.urlAfterRedirects)),
    { initialValue: this.router.url });
  /** The chat page has the whole chat: Flipyap is not on it. */
  readonly onChatPage = computed(() => this.url().split(/[?#]/)[0] === '/chat');

  readonly size = SPRITE_SIZE;
  readonly open = signal(false);
  /** The chat has been opened once: it exists from then on. */
  readonly everOpened = signal(false);
  readonly greeting = signal(false);
  readonly hopping = signal(false);

  private readonly talking = signal(false);
  private readonly blinking = signal(false);
  private readonly mouthOpen = signal(false);
  private readonly reducedMotion = this.isBrowser && matchMedia('(prefers-reduced-motion: reduce)').matches;

  readonly frame = computed<FlipyapFrame>(() => {
    if (this.talking())
      return this.reducedMotion || this.mouthOpen() ? 'talk' : 'idle';
    return this.blinking() ? 'blink' : 'idle';
  });
  readonly pixels = computed(() => FRAMES[this.frame()]);

  constructor() {
    // His mouth opens and shuts while he answers
    effect(onCleanup => {
      if (!this.isBrowser || this.reducedMotion || !this.talking())
        return;
      const beat = setInterval(() => this.mouthOpen.update(open => !open), TALK_BEAT_MS);
      onCleanup(() => {
        clearInterval(beat);
        this.mouthOpen.set(false);
      });
    });

    afterNextRender(() => {
      if (!this.reducedMotion)
        this.blinkNowAndThen();
      this.greetOnce();
    });
  }

  toggle(): void {
    this.greeting.set(false);
    this.hop();
    if (this.open())
      this.close();
    else {
      this.everOpened.set(true);
      this.open.set(true);
      // Already made, he only needs the cursor back in the box
      this.chat()?.focus();
    }
  }

  /** Shuts the chat (the user's button or Escape) and gives the focus back to the pet. */
  close(): void {
    if (!this.open())
      return;
    this.open.set(false);
    this.pet()?.nativeElement.focus();
  }

  onTalking(talking: boolean): void {
    this.talking.set(talking);
  }

  private hop(): void {
    this.hopping.set(true);
    this.later(() => this.hopping.set(false), 400);
  }

  private blinkNowAndThen(): void {
    const timer = setInterval(() => {
      this.blinking.set(true);
      this.later(() => this.blinking.set(false), BLINK_FOR_MS);
    }, BLINK_EVERY_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  private greetOnce(): void {
    try {
      if (sessionStorage.getItem(GREETED_STORAGE_KEY))
        return;
      sessionStorage.setItem(GREETED_STORAGE_KEY, '1');
    } catch {
      // Storage off: he says hello on each page load
    }
    this.greeting.set(true);
    this.later(() => this.greeting.set(false), GREETING_FOR_MS);
  }

  private later(action: () => void, ms: number): void {
    const timer = setTimeout(action, ms);
    this.destroyRef.onDestroy(() => clearTimeout(timer));
  }
}
