import { ApplicationRef, inject, signal, Signal } from '@angular/core';

/** Whether the app has been stable once: from then on, rows are made in the browser, never taken over from the server. */
let stableOnce = false;

/**
 * The animate.enter class for a table's rows (row-enter, styles/_hud.scss), so a new page of rows fades in. None until
 * the app is first stable: until then the rows are the server's, which hydration takes over (with the API answers the
 * server fetched), and they must not fade in a second time. animate.enter takes its class when the row is made, so a
 * row made later fades in, also in a component made later (a client-side navigation). Call it where inject() works,
 * such as a field initializer.
 */
export function rowEnterAnimation(): Signal<string> {
  const name = signal(stableOnce ? 'row-enter' : '');
  if (!stableOnce) {
    inject(ApplicationRef).whenStable().then(() => {
      stableOnce = true;
      name.set('row-enter');
    });
  }
  return name.asReadonly();
}
