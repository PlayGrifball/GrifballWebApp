import { HttpErrorResponse } from '@angular/common/http';
import { catchError, Observable, of, OperatorFunction, throwError } from 'rxjs';

/**
 * A read of one resource whose 404 (there is no such resource) means "none": null instead of an error. With
 * `badRequest`, a 400 too: for reads by an ID that can be malformed (a Waypoint match ID, an XUID), where the server
 * answers 400 for an ID that isn't one, which also means there is no such thing.
 */
export function nullWhenNotFound<T>({ badRequest = false }: { badRequest?: boolean } = {}): OperatorFunction<T, T | null> {
  const none = (status: number) => status === 404 || (badRequest && status === 400);
  return (source: Observable<T>) => source.pipe(
    catchError((error: unknown) => error instanceof HttpErrorResponse && none(error.status) ? of(null) : throwError(() => error)));
}
