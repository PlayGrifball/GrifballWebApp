import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { nullWhenNotFound } from './nullWhenNotFound';

describe('nullWhenNotFound', () => {
  it('passes a value through', () => {
    let seen: string | null | undefined;
    of('x').pipe(nullWhenNotFound()).subscribe(v => seen = v);
    expect(seen).toBe('x');
  });

  it('answers null for a 404', () => {
    let seen: string | null | undefined;
    throwError(() => new HttpErrorResponse({ status: 404 })).pipe(nullWhenNotFound<string>()).subscribe(v => seen = v);
    expect(seen).toBeNull();
  });

  it('passes any other error on', () => {
    let failed: unknown;
    throwError(() => new HttpErrorResponse({ status: 500 })).pipe(nullWhenNotFound<string>()).subscribe({ error: e => failed = e });
    expect((failed as HttpErrorResponse).status).toBe(500);
  });
});
