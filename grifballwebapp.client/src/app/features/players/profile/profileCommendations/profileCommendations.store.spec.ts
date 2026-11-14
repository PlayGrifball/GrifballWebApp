import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ProfileCommendationsDto } from '@api/models';
import { ProfileCommendationsStore } from './profileCommendations.store';

const none = (userID: number): ProfileCommendationsDto => ({ userID, totalMedals: 0, distinctMedals: 0, commendations: [] });

describe('ProfileCommendationsStore', () => {
  let store: ProfileCommendationsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ProfileCommendationsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(ProfileCommendationsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the player', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.data()).toBeNull();
    expect(store.failed()).toBeFalse();
  });

  it("loads the player's commendations, and another player's in their place", async () => {
    const userID = signal(21);
    store.userID.use(userID);
    TestBed.tick();
    http.expectOne('/api/users/21/commendations').flush(none(21));
    await settle();
    expect(store.data()!.userID).toBe(21);

    userID.set(22);
    TestBed.tick();
    http.expectOne('/api/users/22/commendations').flush(none(22));
    await settle();
    expect(store.data()!.userID).toBe(22);
  });

  it('says when they fail to load', async () => {
    store.userID.use(signal(21));
    TestBed.tick();
    http.expectOne('/api/users/21/commendations').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
    expect(store.data()).toBeNull();
  });
});
