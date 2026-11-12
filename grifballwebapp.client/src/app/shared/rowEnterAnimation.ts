import { afterNextRender, signal, Signal } from '@angular/core';

/**
 * The animate.enter class for a table's rows (row-enter, styles/_hud.scss): none until the first render in the
 * browser, so the rows the server drew, which hydration takes over, don't fade in a second time; rows from then on (a
 * new page, a new sort) do. Call it where inject() works, such as a field initializer.
 */
export function rowEnterAnimation(): Signal<string> {
  const name = signal('');
  afterNextRender(() => name.set('row-enter'));
  return name.asReadonly();
}
