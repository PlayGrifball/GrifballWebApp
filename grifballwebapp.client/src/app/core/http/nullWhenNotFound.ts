import { HttpErrorResponse } from '@angular/common/http';
import { catchError, Observable, of, OperatorFunction, throwError } from 'rxjs';

/** A read of one resource whose 404 (there is no such resource) means "none": null instead of an error. */
export function nullWhenNotFound<T>(): OperatorFunction<T, T | null> {
  return (source: Observable<T>) => source.pipe(
    catchError((error: unknown) => error instanceof HttpErrorResponse && error.status === 404 ? of(null) : throwError(() => error)));
}
