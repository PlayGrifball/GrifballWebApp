import { computed, signal, Signal } from '@angular/core';

/**
 * A component's input as its store reads it (docs/frontend.md): the component hands the input's signal over once, in
 * its constructor (`store.userID.use(this.userID)`), and the store's resources and computeds read `value()`, which
 * follows the input from then on, with no effect copying it across. Undefined until handed over: a resource keyed on
 * it stays idle until then.
 */
export class StoreInput<T> {
  private readonly source = signal<Signal<T> | undefined>(undefined);

  /** The input's value, or undefined before the component has handed it over. */
  readonly value = computed<T | undefined>(() => this.source()?.());

  /** Follows `input` from now on. */
  use(input: Signal<T>): void {
    this.source.set(input);
  }
}
