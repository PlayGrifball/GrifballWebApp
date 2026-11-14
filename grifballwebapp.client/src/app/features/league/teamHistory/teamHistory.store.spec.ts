import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { TeamHistoryDto } from '@api/models';
import { TeamHistoryStore } from './teamHistory.store';

const history = { teamName: 'Relentless' } as TeamHistoryDto;

describe('TeamHistoryStore', () => {
  let store: TeamHistoryStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [TeamHistoryStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(TeamHistoryStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the team', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.history()).toBeNull();
  });

  it("loads the franchise's history, and another team's in its place", async () => {
    const teamID = signal(20);
    store.teamID.use(teamID);
    TestBed.tick();
    expect(store.loaded()).toBeFalse();
    http.expectOne('/api/teams/20/history').flush(history);
    await settle();
    expect(store.history()!.teamName).toBe('Relentless');
    expect(store.loaded()).toBeTrue();

    teamID.set(21);
    TestBed.tick();
    expect(store.history()).toBeNull();
    http.expectOne('/api/teams/21/history').flush(null, { status: 404, statusText: 'Not Found' });
    await settle();
    expect(store.history()).toBeNull();
    expect(store.failed()).toBeFalse();
  });

  it('says when it fails to load', async () => {
    store.teamID.use(signal(20));
    TestBed.tick();
    http.expectOne('/api/teams/20/history').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
    expect(store.history()).toBeNull();
  });
});
