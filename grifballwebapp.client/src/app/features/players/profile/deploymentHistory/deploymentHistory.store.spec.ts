import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DeploymentHistoryStore } from './deploymentHistory.store';

describe('DeploymentHistoryStore', () => {
  let store: DeploymentHistoryStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [DeploymentHistoryStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(DeploymentHistoryStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the player', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.deployments()).toEqual([]);
    expect(store.failed()).toBeFalse();
  });

  it('says when the history fails to load, and loads it again on a retry', async () => {
    store.userID.use(signal(21));
    TestBed.tick();
    http.expectOne('/api/users/21/deployment-history').flush('down', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
    expect(store.deployments()).toEqual([]);

    store.reload();
    TestBed.tick();
    http.expectOne('/api/users/21/deployment-history').flush([]);
    await settle();
    expect(store.failed()).toBeFalse();
  });
});
