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
  /** Nothing to show yet: the first answer for these parameters is on its way (not a reload, which keeps the last
   * value shown). What a page shows a skeleton for. */
  readonly firstLoad: Signal<boolean>;
  /** The resource's own status ('idle' before anything is asked for, 'loading', 'resolved', 'error', …). */
  readonly status: Signal<ResourceStatus>;
  /** The last request failed (false again as soon as a retry starts: see `error`). */
  readonly failed: Signal<boolean>;
  /** The last request failed, also while a retry is on its way: what keeps an error shown until an answer comes. */
  readonly failing: Signal<boolean>;
  /** Why the last request failed; kept while a retry is on its way, undefined once one succeeds. */
  readonly error: Signal<Error | undefined>;
  /** The last request answered (for "none found": answered with nothing, as opposed to not asked yet). */
  readonly resolved: Signal<boolean>;
  /** Asks again with the same parameters; false when nothing was asked (the resource is idle). */
  reload(): boolean;
  /** Takes `value` as the answer, without asking (data pushed with a live change: liveData). */
  set(value: V): void;
}

/**
 * A store's resource as the signals its component reads, so stores don't each repeat `hasValue() ? value() : []` and
 * `status() === 'error'`: `private readonly claims = resourceView(rxResource({ … }), [])`, then
 * `readonly rows = this.claims.value`. The fallback is a value of the resource's own type, null or undefined, and the
 * signals' type is exactly that (an `[]` fallback adds no `never[]`). Reading `value` never throws (a resource's own value() does after an error).
 */
export function resourceView<T>(resource: ResourceRef<T>, fallback: NoInfer<Exclude<T, undefined>>): ResourceView<Exclude<T, undefined>>;
export function resourceView<T>(resource: ResourceRef<T>, fallback: null): ResourceView<Exclude<T, undefined> | null>;
export function resourceView<T>(resource: ResourceRef<T>, fallback: undefined): ResourceView<Exclude<T, undefined> | undefined>;
export function resourceView<T>(resource: ResourceRef<T>, fallback: unknown): ResourceView<unknown> {
  return {
    value: computed(() => resource.hasValue() ? resource.value() : fallback),
    hasValue: computed(() => resource.hasValue()),
    isLoading: resource.isLoading,
    firstLoad: computed(() => resource.status() === 'loading'),
    status: resource.status,
    failed: computed(() => resource.status() === 'error'),
    failing: computed(() => resource.error() !== undefined),
    error: resource.error,
    resolved: computed(() => resource.status() === 'resolved'),
    reload: () => resource.reload(),
    set: value => resource.set(value as T),
  };
}
