import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { whileCurrent } from './whileCurrent';

describe('whileCurrent', () => {
  it('passes the answer through while the ID is still the current one', () => {
    const current = signal(1);
    const source = new Subject<string>();
    const seen: string[] = [];
    let completed = false;
    source.pipe(whileCurrent(1, current)).subscribe({ next: v => seen.push(v), complete: () => completed = true });

    source.next('first');
    source.complete();

    expect(seen).toEqual(['first']);
    expect(completed).toBeTrue();
  });

  it('drops an answer that arrives after the ID changed', () => {
    const current = signal(1);
    const source = new Subject<string>();
    const seen: string[] = [];
    source.pipe(whileCurrent(1, current)).subscribe(v => seen.push(v));

    current.set(2);
    source.next('stale');

    expect(seen).toEqual([]);
  });

  it('passes an error through for the current ID', () => {
    const source = new Subject<string>();
    let error: unknown = null;
    source.pipe(whileCurrent(1, () => 1)).subscribe({ error: e => error = e });

    source.error('failed');

    expect(error).toBe('failed');
  });

  it('turns an error for a previous ID into completion', () => {
    const current = signal(1);
    const source = new Subject<string>();
    let error: unknown = null;
    let completed = false;
    source.pipe(whileCurrent(1, current)).subscribe({ error: e => error = e, complete: () => completed = true });

    current.set(2);
    source.error('stale failure');

    expect(error).toBeNull();
    expect(completed).toBeTrue();
  });
});
