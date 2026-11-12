import { ApplicationRef, inject, Injectable, signal, Signal } from '@angular/core';
import { first } from 'rxjs';

/**
 * The animate.enter class for a table's rows (row-enter, styles/_hud.scss), so a new page of rows fades in. None until
 * the app is first stable: until then the rows are the server's, which hydration takes over (with the API answers the
 * server fetched), and they must not fade in a second time. One for the app (root), so once it has been stable, a
 * table made later (a client-side navigation) fades its rows in from its first render.
 */
@Injectable({ providedIn: 'root' })
export class RowEnterAnimation {
  private readonly name = signal('');
  readonly className = this.name.asReadonly();

  constructor() {
    // isStable tells its current value at once: already stable, the class is there before any row is made
    inject(ApplicationRef).isStable.pipe(first(stable => stable)).subscribe(() => this.name.set('row-enter'));
  }
}

/** The rows' animate.enter class (RowEnterAnimation). Call it where inject() works, such as a field initializer. */
export function rowEnterAnimation(): Signal<string> {
  return inject(RowEnterAnimation).className;
}
