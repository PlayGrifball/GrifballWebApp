import { ApplicationRef, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { resourceView } from './resourceView';

describe('resourceView', () => {
  /** A resource answering from a Subject per request, so a test says when and how each request answers. */
  function setUp() {
    const answers: Subject<number[]>[] = [];
    const params = signal<number | undefined>(undefined);
    const view = TestBed.runInInjectionContext(() => resourceView(rxResource({
      params: () => params(),
      stream: () => {
        const answer = new Subject<number[]>();
        answers.push(answer);
        return answer;
      },
    }), [] as number[]));
    return { params, answers, view };
  }
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('gives the fallback before anything is asked for, and nothing is loading, failed or resolved', () => {
    const { view } = setUp();
    TestBed.tick();
    expect(view.status()).toBe('idle');
    expect(view.value()).toEqual([]);
    expect(view.hasValue()).toBeFalse();
    expect(view.isLoading()).toBeFalse();
    expect(view.failed()).toBeFalse();
    expect(view.resolved()).toBeFalse();
  });

  it('is loading while a request is on its way, then gives its value', async () => {
    const { params, view, answers } = setUp();
    params.set(1);
    TestBed.tick();
    expect(view.isLoading()).toBeTrue();
    expect(view.value()).toEqual([]);
    answers[0].next([1, 2]);
    answers[0].complete();
    await settle();
    expect(view.isLoading()).toBeFalse();
    expect(view.hasValue()).toBeTrue();
    expect(view.resolved()).toBeTrue();
    expect(view.value()).toEqual([1, 2]);
  });

  it('gives the fallback, never a throw, after a failure, and says why', async () => {
    const { params, view, answers } = setUp();
    params.set(1);
    TestBed.tick();
    answers[0].error(new Error('down'));
    await settle();
    expect(view.failed()).toBeTrue();
    expect(view.error()!.message).toBe('down');
    expect(view.value()).toEqual([]);
    expect(view.hasValue()).toBeFalse();
    expect(view.resolved()).toBeFalse();
  });

  it('asks again on a reload, keeping the parameters', async () => {
    const { params, view, answers } = setUp();
    params.set(1);
    TestBed.tick();
    answers[0].next([1]);
    answers[0].complete();
    await settle();
    view.reload();
    TestBed.tick();
    expect(answers).toHaveSize(2);
    answers[1].next([1, 3]);
    answers[1].complete();
    await settle();
    expect(view.value()).toEqual([1, 3]);
  });

  it('keeps the last value while reloading it, and the last error while retrying after one', async () => {
    const { params, view, answers } = setUp();
    params.set(1);
    TestBed.tick();
    answers[0].error(new Error('down'));
    await settle();
    view.reload();
    TestBed.tick();
    // Angular's reloading: no longer "error", but the error it had is still there until the retry answers
    expect(view.failed()).toBeFalse();
    expect(view.error()!.message).toBe('down');
    answers[1].next([5]);
    answers[1].complete();
    await settle();
    expect(view.error()).toBeUndefined();

    view.reload();
    TestBed.tick();
    expect(view.isLoading()).toBeTrue();
    expect(view.hasValue()).toBeTrue();
    expect(view.value()).toEqual([5]);
    answers[2].next([6]);
    answers[2].complete();
    await settle();
  });
});
