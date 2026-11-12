import { ApplicationRef, EnvironmentProviders, inject, Injectable, provideAppInitializer, signal, Signal } from '@angular/core';
import { first } from 'rxjs';

/**
 * The animate.enter class for a table's rows (row-enter, styles/_hud.scss), so a new page of rows fades in. None until
 * the app is first stable: until then the rows are the server's, which hydration takes over (with the API answers the
 * server fetched), and they must not fade in a second time. One for the app (root), made at bootstrap
 * (provideRowEnterAnimation): made later, by the first table, it would miss the app's first stable point when that table
 * comes by a client-side navigation, which keeps the app busy while the table is made.
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

/** Makes RowEnterAnimation as the app starts (app.config), before anything can keep the app busy. */
export function provideRowEnterAnimation(): EnvironmentProviders {
  return provideAppInitializer(() => {
    inject(RowEnterAnimation);
  });
}

/** The rows' animate.enter class (RowEnterAnimation). Call it where inject() works, such as a field initializer. */
export function rowEnterAnimation(): Signal<string> {
  return inject(RowEnterAnimation).className;
}
