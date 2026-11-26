import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { SeasonSchedulingRulesDto } from '@api/models';
import { SchedulingRulesStore } from './schedulingRules.store';

describe('SchedulingRulesStore', () => {
  let store: SchedulingRulesStore;
  let http: HttpTestingController;
  const rules: SeasonSchedulingRulesDto = { minPlayersAvailable: 4, maxMatchesPerTeamPerNight: 1, maxMatchesPerTeamPerWeek: 2, maxConcurrentMatches: null };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SchedulingRulesStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(SchedulingRulesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it("loads the season's rules, with a first load meanwhile", async () => {
    store.seasonID.use(signal(3));
    TestBed.tick();
    expect(store.firstLoad()).toBeTrue();
    http.expectOne({ method: 'GET', url: '/api/seasons/3/scheduling-rules' }).flush(rules);
    await settle();
    expect(store.firstLoad()).toBeFalse();
    expect(store.rules()).toEqual(rules);
  });

  it('fails, with no rules and no skeleton, when they cannot be read', async () => {
    store.seasonID.use(signal(3));
    TestBed.tick();
    http.expectOne('/api/seasons/3/scheduling-rules').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.firstLoad()).toBeFalse();
    expect(store.failed()).toBeTrue();
    expect(store.rules()).toBeNull();
  });

  it('saves them, and says so', () => {
    store.save(3, { ...rules, minPlayersAvailable: 3 });
    expect(store.saving()).toBeTrue();
    const put = http.expectOne({ method: 'POST', url: '/api/seasons/3/scheduling-rules' });
    expect(put.request.body.minPlayersAvailable).toBe(3);
    put.flush(null);
    expect(store.saving()).toBeFalse();
    expect(store.saved()).toBeTrue();
    expect(store.error()).toBeNull();
    expect(store.rules()!.minPlayersAvailable).toBe(3);
  });

  it("keeps the server's reason when it refuses", () => {
    store.save(3, { ...rules, maxMatchesPerTeamPerWeek: 0 });
    http.expectOne('/api/seasons/3/scheduling-rules').flush(
      { title: 'Bad Request', status: 400, detail: 'A team plays at least as many matches a week as a night.' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    expect(store.error()).toBe('A team plays at least as many matches a week as a night.');
    expect(store.saved()).toBeFalse();
  });
});
