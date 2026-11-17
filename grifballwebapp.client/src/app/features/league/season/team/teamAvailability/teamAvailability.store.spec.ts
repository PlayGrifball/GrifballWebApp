import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { TeamSlots } from '@api/models';
import { TeamAvailabilityStore } from './teamAvailability.store';

describe('TeamAvailabilityStore', () => {
  let store: TeamAvailabilityStore;
  let http: HttpTestingController;
  const slots: TeamSlots = {
    teamID: 7, setByCaptain: false, rosterSize: 5,
    slots: [
      { availabilityOptionID: 1, dayOfWeek: 'Monday', time: '20:00:00', available: true, playersAvailable: 5 },
      { availabilityOptionID: 2, dayOfWeek: 'Tuesday', time: '20:00:00', available: false, playersAvailable: 2 },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [TeamAvailabilityStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(TeamAvailabilityStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads the team's timeslots", () => {
    store.load(7);
    http.expectOne({ method: 'GET', url: '/api/teams/7/availability' }).flush(slots);
    expect(store.slots()).toEqual(slots);
  });

  it('saves the captain\'s own, then shows them', () => {
    store.save(7, [2]);
    expect(store.saving()).toBeTrue();
    const put = http.expectOne({ method: 'PUT', url: '/api/teams/7/availability' });
    expect(put.request.body).toEqual({ availabilityOptionIDs: [2] });
    put.flush(null);
    http.expectOne('/api/teams/7/availability').flush({ ...slots, setByCaptain: true });
    expect(store.saving()).toBeFalse();
    expect(store.slots()!.setByCaptain).toBeTrue();
  });

  it("resets them to the roster's, and says why when refused", () => {
    store.reset(7);
    http.expectOne({ method: 'DELETE', url: '/api/teams/7/availability' }).flush(
      { title: 'Forbidden', status: 403, detail: "Only the team's captain (or staff) can change its availability." },
      { status: 403, statusText: 'Forbidden', headers: { 'Content-Type': 'application/problem+json' } });
    expect(store.error()).toBe("Only the team's captain (or staff) can change its availability.");
    expect(store.saving()).toBeFalse();
  });
});
