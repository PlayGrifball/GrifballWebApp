import { computed, ResourceRef, ResourceStatus, Signal } from '@angular/core';

/** One of a store's loads, as signals (docs/frontend.md); the store keeps it private and gives its signals out. */
export interface ResourceView<V> {
  /** The value once loaded; the fallback before anything is asked for, while the first load is on its way, and after
   * a failure. A reload keeps the last value until the next one arrives. */
  readonly value: Signal<V>;
  /** A value has been loaded (still true while it is being reloaded). */
  readonly hasValue: Signal<boolean>;
  /** A request is on its way (the first, or a reload). */
  readonly isLoading: Signal<boolean>;
  /** The resource's own status ('idle' before anything is asked for, 'loading', 'resolved', 'error', …). */
  readonly status: Signal<ResourceStatus>;
  /** The last request failed (false again as soon as a retry starts: see `error`). */
  readonly failed: Signal<boolean>;
  /** Why the last request failed; kept while a retry is on its way, undefined once one succeeds. */
  readonly error: Signal<Error | undefined>;
  /** The last request answered (for "none found": answered with nothing, as opposed to not asked yet). */
  readonly resolved: Signal<boolean>;
  /** Asks again with the same parameters. */
  reload(): void;
}

/**
 * A store's resource as the signals its component reads, so stores don't each repeat `hasValue() ? value() : []` and
 * `status() === 'error'`: `private readonly claims = resourceView(rxResource({ … }), [])`, then
 * `readonly rows = this.claims.value`. The fallback is checked against the resource's type (a value, null or
 * undefined). Reading `value` never throws (a resource's own value() does after an error).
 */
export function resourceView<T, F extends NoInfer<Exclude<T, undefined>> | null | undefined>(
  resource: ResourceRef<T>, fallback: F): ResourceView<Exclude<T, undefined> | F> {
  return {
    value: computed(() => resource.hasValue() ? resource.value() as Exclude<T, undefined> : fallback),
    hasValue: computed(() => resource.hasValue()),
    isLoading: resource.isLoading,
    status: resource.status,
    failed: computed(() => resource.status() === 'error'),
    error: resource.error,
    resolved: computed(() => resource.status() === 'resolved'),
    reload: () => { resource.reload(); },
  };
}
