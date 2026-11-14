import { HttpErrorResponse, provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { MergeRequest } from '@api/models';
import { MergeUserStore } from './mergeUser.store';

const user = (userID: number) => ({ userID, userName: `user${userID}`, lockoutEnd: null });
const request = { mergeFromId: 5, mergeToId: 6, mergeOptions: {} } as MergeRequest;

describe('MergeUserStore', () => {
  let store: MergeUserStore;
  let http: HttpTestingController;

  beforeEach(() => {
    spyOn(console, 'log');
    TestBed.configureTestingModule({ providers: [MergeUserStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(MergeUserStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('looks nobody up until an ID is chosen', () => {
    store.fromID.use(signal(0));
    store.toID.use(signal(0));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.fromUser()).toBeUndefined();
  });

  it('looks both users up, and tells no such user from a failed lookup', async () => {
    const fromID = signal(5);
    store.fromID.use(fromID);
    store.toID.use(signal(6));
    TestBed.tick();
    http.expectOne('/api/users/5').flush(user(5));
    http.expectOne('/api/users/6').flush('x', { status: 404, statusText: 'Not Found' });
    await settle();
    expect(store.fromUser()!.userID).toBe(5);
    expect(store.toUser()).toBeUndefined();
    expect((store.toError() as HttpErrorResponse).status).toBe(404);
    expect(store.fromError()).toBeUndefined();

    fromID.set(7);
    TestBed.tick();
    http.expectOne('/api/users/7').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.fromUser()).toBeUndefined();
    expect((store.fromError() as HttpErrorResponse).status).toBe(500);
  });

  it('merges the users, and says how it went', () => {
    store.merge(request);
    expect(store.submitting()).toBeTrue();
    expect(store.result()).toBeNull();
    const merge = http.expectOne({ method: 'POST', url: '/api/user-merges' });
    expect(merge.request.body).toEqual(request);
    merge.flush(null);
    expect(store.submitting()).toBeFalse();
    expect(store.result()).toEqual({ ok: true, text: 'Merged user 5 into 6' });

    store.merge(request);
    expect(store.result()).toBeNull();
    http.expectOne('/api/user-merges').flush({ title: 'Refused', detail: 'Same user' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    expect(store.submitting()).toBeFalse();
    expect(store.result()).toEqual({ ok: false, text: 'Same user' });
  });
});
