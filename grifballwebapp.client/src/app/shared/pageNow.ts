import { PLATFORM_ID, TransferState, WritableSignal, afterNextRender, inject, makeStateKey, signal } from '@angular/core';
import { isPlatformServer } from '@angular/common';

const SERVER_NOW = makeStateKey<number>('pageNow');

/**
 * "Now" (ms since the epoch) for what a page shows by the clock, such as a Sign up button only
 * while signups are open. The server draws the page by its clock and sends that time with it; the
 * browser takes the page over with the same time, so both draw the same thing, then switches to
 * its own clock once it has (a few seconds' drift may then show or hide the button). A page drawn
 * in the browser alone uses the browser's clock from the start. Call in an injection context.
 */
export function pageNow(): WritableSignal<number> {
  const state = inject(TransferState);
  if (isPlatformServer(inject(PLATFORM_ID))) {
    const now = state.get(SERVER_NOW, Date.now());
    state.set(SERVER_NOW, now);
    return signal(now);
  }
  if (!state.hasKey(SERVER_NOW))
    return signal(Date.now());
  const now = signal(state.get(SERVER_NOW, Date.now()));
  afterNextRender(() => now.set(Date.now()));
  return now;
}
