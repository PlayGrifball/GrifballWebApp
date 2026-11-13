import { map, type OperatorFunction } from 'rxjs';

/**
 * A read whose 200 has a body and whose 204 has none (the generated client types it by the 200 alone): the 204 as null.
 */
export function noContentAsNull<T>(): OperatorFunction<T, T | null> {
  return map(body => body ?? null);
}
