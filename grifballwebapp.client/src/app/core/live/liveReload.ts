import { DestroyRef, effect, inject, untracked } from '@angular/core';
import { LiveMessage, LiveService } from './live.service';

/** How long a page waits after a change before it loads again: a burst of changes (a draft's picks) is one load. */
export const LIVE_RELOAD_MS = 300;

/**
 * And up to how much longer, at random: the pages watching a topic all hear of its change at once, so they spread their
 * reads over this rather than all asking the API in the same moment.
 */
export const LIVE_SPREAD_MS = 2_000;

/**
 * Keeps what a store shows live: `reload` runs whenever one of `topics` changes, and when the live connection comes
 * back after a drop, once a burst of changes settles ({@link LIVE_RELOAD_MS}) and a random while more
 * ({@link LIVE_SPREAD_MS}). `topics` is read as a signal: a store whose route changes (another season) watches the new
 * one's. None (an empty list) watches nothing. Call it in an injection context, a store's constructor; it stops with
 * the store. For a topic that comes with its data, {@link liveData}.
 */
export function liveReload(topics: () => readonly string[], reload: () => void): void {
  liveData(topics, () => false, reload);
}

/**
 * As {@link liveReload}, for a topic many watch, whose change comes with its data (the server's LiveSnapshots payload,
 * `T` by the topic): `apply` takes it at once, with no read, and answers whether it could (false: `reload` reads it
 * instead, as for a change without it, or the connection coming back).
 */
export function liveData<T>(topics: () => readonly string[], apply: (data: T) => boolean, reload: () => void): void {
  const live = inject(LiveService);
  let timer: ReturnType<typeof setTimeout> | undefined;
  const changed = (message: LiveMessage) => {
    clearTimeout(timer);
    if (message.data !== undefined && apply(message.data as T))
      return;
    // eslint-disable-next-line sonarjs/pseudo-random -- only spreads the pages' reads over time: nothing depends on it being unpredictable
    timer = setTimeout(reload, LIVE_RELOAD_MS + Math.random() * LIVE_SPREAD_MS);
  };
  let stop: (() => void) | undefined;
  effect(() => {
    const watched = topics();
    untracked(() => {
      stop?.();
      stop = watched.length > 0 ? live.watch(watched, changed) : undefined;
    });
  });
  inject(DestroyRef).onDestroy(() => {
    stop?.();
    clearTimeout(timer);
  });
}

