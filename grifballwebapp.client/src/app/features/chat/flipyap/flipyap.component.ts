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
/** Where he was put down, kept for the next visit. */
export const POSITION_STORAGE_KEY = 'flipyap-position';
/** How far a press must move before it is a drag and not a click. */
export const DRAG_THRESHOLD_PX = 4;
/** His distance from the window's right and bottom edges before anyone moves him. */
export const CORNER_MARGIN_PX = 16;

interface Offset {
  readonly x: number;
  readonly y: number;
}

interface Drag {
  readonly pointerId: number;
  readonly startX: number;
  readonly startY: number;
  readonly from: Offset;
  moved: boolean;
}

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
  host: { '(keydown.escape)': 'close()', '(window:resize)': 'keepOnScreen()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FlipyapComponent {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  /** The box he stands in: still while he bobs and swings, so it is what stays inside the window. */
  private readonly box = viewChild<ElementRef<HTMLElement>>('box');
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

  readonly margin = CORNER_MARGIN_PX;
  /** How far he has been moved from the corner, to the right (x) and down (y): both zero or less in a window he fits. */
  readonly offset = signal<Offset>({ x: 0, y: 0 });
  /** He is in the user's hand. */
  readonly held = signal(false);
  /** In the top or left half of the window, the chat opens below him or to his right, where there is room. */
  readonly nearTop = signal(false);
  readonly nearLeft = signal(false);
  private drag: Drag | null = null;
  private suppressClick = false;

  readonly frame = computed<FlipyapFrame>(() => {
    if (this.held())
      return 'held';
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
      this.restorePosition();
    });
  }

  toggle(): void {
    // The click that ends a drag is not a tap
    if (this.suppressClick)
      return;
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

  onPointerDown(event: PointerEvent): void {
    if (event.button !== 0)
      return;
    this.drag = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, from: this.offset(), moved: false };
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }

  onPointerMove(event: PointerEvent): void {
    const drag = this.drag;
    if (drag?.pointerId !== event.pointerId)
      return;
    const dx = event.clientX - drag.startX;
    const dy = event.clientY - drag.startY;
    if (!drag.moved) {
      if (Math.hypot(dx, dy) < DRAG_THRESHOLD_PX)
        return;
      drag.moved = true;
      this.held.set(true);
    }
    this.moveTo(drag.from.x + dx, drag.from.y + dy);
  }

  /** Puts him down where he is: a drag keeps its place, and its click is not a tap. */
  onPointerEnd(event: PointerEvent): void {
    const drag = this.drag;
    if (drag?.pointerId !== event.pointerId)
      return;
    this.drag = null;
    if (!drag.moved)
      return;
    this.held.set(false);
    this.hop();
    this.suppressClick = true;
    this.later(() => (this.suppressClick = false), 0);
    try {
      localStorage.setItem(POSITION_STORAGE_KEY, JSON.stringify(this.offset()));
    } catch {
      // Storage off: he is back in the corner next visit
    }
  }

  /** The window changed size: he is pulled back in if it left him outside. */
  keepOnScreen(): void {
    const { x, y } = this.offset();
    this.moveTo(x, y);
  }

  /** Moves him to the offset, or as near it as keeps all of him in the window. */
  private moveTo(x: number, y: number): void {
    const box = this.box()?.nativeElement;
    if (!box)
      return;
    const rect = box.getBoundingClientRect();
    const now = this.offset();
    const clamp = (delta: number, low: number, high: number) => Math.min(Math.max(delta, low), high);
    const dx = clamp(x - now.x, -rect.left, innerWidth - rect.right);
    const dy = clamp(y - now.y, -rect.top, innerHeight - rect.bottom);
    this.offset.set({ x: now.x + dx, y: now.y + dy });
    this.nearLeft.set(rect.left + dx + rect.width / 2 < innerWidth / 2);
    this.nearTop.set(rect.top + dy + rect.height / 2 < innerHeight / 2);
  }

  private restorePosition(): void {
    try {
      const kept: unknown = JSON.parse(localStorage.getItem(POSITION_STORAGE_KEY) ?? 'null');
      const { x, y } = (kept ?? {}) as Partial<Offset>;
      if (typeof x === 'number' && typeof y === 'number' && Number.isFinite(x) && Number.isFinite(y))
        this.moveTo(x, y);
    } catch {
      // Nothing kept, or not his position: he stays in the corner
    }
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
