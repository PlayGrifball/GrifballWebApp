import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { UserResponseDto } from '@api/models';
import { EditUserStore } from './editUser.store';

const user = (userID: number, lockoutEnd: string | null = null) => ({ userID, userName: `user${userID}`, lockoutEnd }) as unknown as UserResponseDto;

describe('EditUserStore', () => {
  let store: EditUserStore;
  let http: HttpTestingController;

  beforeEach(() => {
    spyOn(console, 'log');
    TestBed.configureTestingModule({ providers: [EditUserStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(EditUserStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for nothing until it has the user, and nothing for none', () => {
    TestBed.tick();
    http.expectNone(() => true);
    store.userID.use(signal(0));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.loaded()).toBeNull();
  });

  it('loads the user, their lockout end read and kept as sent', () => {
    store.userID.use(signal(5));
    TestBed.tick();
    http.expectOne('/api/users/5').flush(user(5, '9999-12-31T23:59:59.9999999+00:00'));
    expect(store.loaded()!.user.userID).toBe(5);
    expect(store.loaded()!.user.lockoutEnd).not.toBeNull();
    expect(store.loaded()!.rawLockoutEnd).toBe('9999-12-31T23:59:59.9999999+00:00');
  });

  it('drops the last user for another, and the late answer for them, and keeps none for no such user', () => {
    const userID = signal(5);
    store.userID.use(userID);
    TestBed.tick();
    const late = http.expectOne('/api/users/5');
    userID.set(6);
    TestBed.tick();
    late.flush(user(5));
    expect(store.loaded()).toBeNull();
    http.expectOne('/api/users/6').flush(null);
    expect(store.loaded()).toBeNull();

    userID.set(7);
    TestBed.tick();
    http.expectOne('/api/users/7').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeNull();
  });

  it('saves the user, logging how it went', () => {
    store.save(5, user(5));
    const save = http.expectOne({ method: 'POST', url: '/api/users/5' });
    expect(save.request.body).toEqual(user(5));
    save.flush(null);
    expect(console.log).toHaveBeenCalledWith('Updated user');

    store.save(5, user(5));
    http.expectOne('/api/users/5').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(console.log).toHaveBeenCalledTimes(2);
  });
});
