import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { UserResponseDto } from '@api/models';
import { EditUserStore } from './editUser.store';

const user = (userID: number, lockoutEnd: string | null = null) => ({ userID, userName: `user${userID}`, lockoutEnd }) as unknown as UserResponseDto;

describe('EditUserStore', () => {
  let store: EditUserStore;
  let http: HttpTestingController;

  beforeEach(() => {
    spyOn(console, 'info');
    spyOn(console, 'error');
    TestBed.configureTestingModule({ providers: [EditUserStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(EditUserStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the user, and nothing for none', async () => {
    TestBed.tick();
    http.expectNone(() => true);
    store.userID.use(signal(0));
    await settle();
    http.expectNone(() => true);
    expect(store.loaded()).toBeNull();
    expect(store.firstLoad()).toBeFalse();
  });

  it('loads the user, their lockout end read and kept as sent', async () => {
    store.userID.use(signal(5));
    TestBed.tick();
    expect(store.firstLoad()).toBeTrue();
    http.expectOne('/api/users/5').flush(user(5, '9999-12-31T23:59:59.9999999+00:00'));
    await settle();
    expect(store.firstLoad()).toBeFalse();
    expect(store.loaded()!.user.userID).toBe(5);
    expect(store.loaded()!.user.lockoutEnd).not.toBeNull();
    expect(store.loaded()!.rawLockoutEnd).toBe('9999-12-31T23:59:59.9999999+00:00');
  });

  it('drops the last user for another, cancelling their request, and keeps none for no such user', async () => {
    const userID = signal(5);
    store.userID.use(userID);
    TestBed.tick();
    const late = http.expectOne('/api/users/5');
    userID.set(6);
    TestBed.tick();
    expect(late.cancelled).toBeTrue();
    http.expectOne('/api/users/6').flush(null);
    await settle();
    expect(store.loaded()).toBeNull();
    expect(store.failed()).toBeFalse();
  });

  it('fails, rather than giving an empty user to edit, when the user cannot be loaded, and logs it', async () => {
    store.userID.use(signal(5));
    TestBed.tick();
    http.expectOne('/api/users/5').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.firstLoad()).toBeFalse();
    expect(store.failed()).toBeTrue();
    expect(store.loaded()).toBeNull();
    expect(console.error).toHaveBeenCalledWith('Failed to load the user', jasmine.anything());
  });

  it("is not waiting once the route has no user, even with a user's request on its way", () => {
    const userID = signal(5);
    store.userID.use(userID);
    TestBed.tick();
    const late = http.expectOne('/api/users/5');
    userID.set(0);
    // Synchronous: right after a cancelled load, whenStable would wait on it
    TestBed.tick();
    expect(late.cancelled).toBeTrue();
    expect(store.firstLoad()).toBeFalse();
  });

  it('saves the user, logging how it went', () => {
    store.save(5, user(5));
    const save = http.expectOne({ method: 'POST', url: '/api/users/5' });
    expect(save.request.body).toEqual(user(5));
    save.flush(null);
    expect(console.info).toHaveBeenCalledWith('Updated user 5');

    store.save(5, user(5));
    http.expectOne('/api/users/5').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(console.error).toHaveBeenCalledWith('Failed to update the user', jasmine.anything());
  });
});
