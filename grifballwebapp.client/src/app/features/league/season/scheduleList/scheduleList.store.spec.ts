import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { OnDemandJobStatusDto, ScheduleRecommendationPageDto } from '@api/models';
import { DateTime } from 'luxon';
import { ScheduleListStore } from './scheduleList.store';

const idleRun: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };

describe('ScheduleListStore', () => {
  let store: ScheduleListStore;
  let http: HttpTestingController;

  const unscheduled = (id: number) => ({ seasonMatchID: id, homeCaptain: 'H', awayCaptain: 'A', homeTeam: 'Home', awayTeam: 'Away', complete: false });
  const scheduled = (id: number, time: string) => ({ ...unscheduled(id), time });

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ScheduleListStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(ScheduleListStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Shows season 4 and answers both lists. */
  const show = () => {
    store.show(4);
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([unscheduled(9)]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([scheduled(1, '2026-01-07T19:00:00Z')]);
  };

  it('loads both lists, the scheduled one by week, and is loaded once both are in', () => {
    store.show(4);
    expect(store.loaded()).toBeFalse();
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([unscheduled(9)]);
    expect(store.loaded()).toBeFalse();
    http.expectOne('/api/seasons/4/matches/scheduled').flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeTrue();
    expect(store.unscheduledMatches().map(m => m.seasonMatchID)).toEqual([9]);
    expect(store.scheduledMatches()).toEqual([]);
  });

  it('asks for nothing without a season', () => {
    store.show(0);
    http.expectNone(() => true);
    expect(store.loaded()).toBeFalse();
  });

  it('sets a match\'s time, or none, and loads the schedule again whatever the answer', () => {
    show();
    store.setTime(9, DateTime.fromISO('2026-01-08T20:00:00Z'));
    const set = http.expectOne({ method: 'POST', url: '/api/season-matches/9/time' });
    expect(set.request.body.seasonMatchID).toBe(9);
    expect(DateTime.fromISO(set.request.body.time).toMillis()).toBe(DateTime.fromISO('2026-01-08T20:00:00Z').toMillis());
    set.flush(null, { status: 400, statusText: 'Bad Request' });
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);

    store.setTime(9, null);
    expect(http.expectOne('/api/season-matches/9/time').request.body.time).toBeNull();
  });

  it('applies the latest recommendation, then reads the schedule and the recommendation again', () => {
    show();
    const page: ScheduleRecommendationPageDto = {
      run: idleRun,
      latest: {
        scheduleRecommendationID: 12, createdAt: '2026-11-01T18:00:00Z', requestedBy: 'Grif', fitness: 0.5, generations: 10,
        appliedAt: null, appliedBy: null, matches: [],
      },
    };
    store.loadRecommendation();
    http.expectOne('/api/seasons/4/schedule-recommendation').flush(page);

    store.apply();
    http.expectOne({ method: 'POST', url: '/api/schedule-recommendations/12/apply' }).flush(null);
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
    http.expectOne('/api/seasons/4/schedule-recommendation').flush({ ...page, latest: { ...page.latest!, appliedBy: 'Grif' } });
    expect(store.recommendation()!.latest!.appliedBy).toBe('Grif');
  });

  it('drops the previous season\'s recommendation and a late answer for it', () => {
    show();
    store.loadRecommendation();
    const late = http.expectOne('/api/seasons/4/schedule-recommendation');
    store.show(0);
    late.flush({ run: idleRun, latest: null });
    expect(store.recommendation()).toBeNull();
  });
});
