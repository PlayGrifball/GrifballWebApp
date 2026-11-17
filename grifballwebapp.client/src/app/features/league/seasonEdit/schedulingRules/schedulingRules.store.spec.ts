import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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

  it("loads the season's rules", () => {
    store.load(3);
    http.expectOne({ method: 'GET', url: '/api/seasons/3/scheduling-rules' }).flush(rules);
    expect(store.rules()).toEqual(rules);
  });

  it('saves them, and says so', () => {
    store.save(3, { ...rules, minPlayersAvailable: 3 });
    expect(store.saving()).toBeTrue();
    const put = http.expectOne({ method: 'PUT', url: '/api/seasons/3/scheduling-rules' });
    expect(put.request.body.minPlayersAvailable).toBe(3);
    put.flush(null);
    expect(store.saving()).toBeFalse();
    expect(store.saved()).toBeTrue();
    expect(store.error()).toBeNull();
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
