import { PendingTasks } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { rowEnterAnimation } from './rowEnterAnimation';

describe('rowEnterAnimation', () => {
  it('fades rows in at once for a table made while the app is stable', () => {
    const name = TestBed.runInInjectionContext(() => rowEnterAnimation());
    expect(name()).toBe('row-enter');
  });

  it('fades none in while the app is not yet stable (hydrating the server\'s rows), then fades in the ones after', async () => {
    const done = TestBed.inject(PendingTasks).add();
    const name = TestBed.runInInjectionContext(() => rowEnterAnimation());
    expect(name()).toBe('');

    done();
    await new Promise(resolve => setTimeout(resolve));
    expect(name()).toBe('row-enter');
  });

  it('once the app has been stable, fades in the rows of every table made after, even while the app is busy again', async () => {
    const first = TestBed.inject(PendingTasks).add();
    TestBed.runInInjectionContext(() => rowEnterAnimation());
    first();
    await new Promise(resolve => setTimeout(resolve));

    TestBed.inject(PendingTasks).add(); // busy again, as a navigation loading its page
    const later = TestBed.runInInjectionContext(() => rowEnterAnimation());
    expect(later()).toBe('row-enter');
  });
});
