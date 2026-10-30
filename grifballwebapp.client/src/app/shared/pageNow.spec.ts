import { ApplicationRef, Injector, PLATFORM_ID, TransferState, makeStateKey, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { pageNow } from './pageNow';

const KEY = makeStateKey<number>('pageNow');

describe('pageNow', () => {
  const create = (platform: string) => {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
    return runInInjectionContext(TestBed.inject(Injector), () => pageNow());
  };

  it('on the server, is the server\'s clock, sent with the page', () => {
    const before = Date.now();
    const now = create('server')();
    expect(now).toBeGreaterThanOrEqual(before);
    expect(TestBed.inject(TransferState).get(KEY, 0)).toBe(now);
  });

  it('on the server, is the same for every part of the page', () => {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: 'server' }] });
    TestBed.inject(TransferState).set(KEY, 1234);
    expect(runInInjectionContext(TestBed.inject(Injector), () => pageNow())()).toBe(1234);
  });

  it('in the browser, takes over with the server\'s time, then follows its own clock once drawn', async () => {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: 'browser' }] });
    TestBed.inject(TransferState).set(KEY, 1234);
    const now = runInInjectionContext(TestBed.inject(Injector), () => pageNow());
    expect(now()).toBe(1234);

    TestBed.inject(ApplicationRef).tick();
    await TestBed.inject(ApplicationRef).whenStable();
    expect(now()).toBeGreaterThan(1234);
  });

  it('in the browser, drops the server\'s time once drawn, so later pages use the browser\'s clock', async () => {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: 'browser' }] });
    const state = TestBed.inject(TransferState);
    state.set(KEY, 1234);
    const injector = TestBed.inject(Injector);
    const first = runInInjectionContext(injector, () => pageNow());
    // Another part of the same first page, before it is drawn, gets the same time
    const sibling = runInInjectionContext(injector, () => pageNow());
    expect(first()).toBe(1234);
    expect(sibling()).toBe(1234);

    TestBed.inject(ApplicationRef).tick();
    await TestBed.inject(ApplicationRef).whenStable();
    expect(state.hasKey(KEY)).toBeFalse();

    // A page navigated to afterwards
    const before = Date.now();
    expect(runInInjectionContext(injector, () => pageNow())()).toBeGreaterThanOrEqual(before);
  });

  it('in the browser without a page from the server, is the browser\'s clock', () => {
    const before = Date.now();
    expect(create('browser')()).toBeGreaterThanOrEqual(before);
  });
});
