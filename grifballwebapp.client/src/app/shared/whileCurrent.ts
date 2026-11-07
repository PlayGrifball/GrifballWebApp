import { untracked } from '@angular/core';
import { Observable, type MonoTypeOperatorFunction } from 'rxjs';

/**
 * For a page that loads by an ID from its route: lets an answer through only while the page still shows the ID it was
 * asked for. Angular keeps the same page when only the ID in its route changes (Profile in the header while on someone
 * else's profile), so a slow answer, or error, for the previous ID would otherwise land on the new one. It is dropped
 * instead (an error then just completes).
 */
export function whileCurrent<T>(id: unknown, current: () => unknown): MonoTypeOperatorFunction<T> {
  const isCurrent = () => untracked(current) === id;
  return source => new Observable<T>(subscriber => source.subscribe({
    next: value => { if (isCurrent()) subscriber.next(value); },
    error: error => isCurrent() ? subscriber.error(error) : subscriber.complete(),
    complete: () => subscriber.complete(),
  }));
}
