import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SeasonAvailabilityStore } from './seasonAvailability.store';

describe('SeasonAvailabilityStore', () => {
  let store: SeasonAvailabilityStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [SeasonAvailabilityStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(SeasonAvailabilityStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it("loads the season's timeslots, with a first load meanwhile", async () => {
    store.seasonID.use(signal(14));
    TestBed.tick();
    expect(store.firstLoad()).toBeTrue();
    http.expectOne({ method: 'GET', url: '/api/seasons/14/availability' }).flush([{ dayOfWeek: 'Monday', time: '19:00:00' }]);
    await settle();
    expect(store.firstLoad()).toBeFalse();
    expect(store.timeslots()).toEqual([{ dayOfWeek: 'Monday', time: '19:00:00' }]);
  });

  it('says when it cannot load them, and fails rather than waiting', async () => {
    store.seasonID.use(signal(14));
    TestBed.tick();
    http.expectOne('/api/seasons/14/availability').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to get season availability');
    expect(store.firstLoad()).toBeFalse();
    expect(store.failed()).toBeTrue();
  });

  it('saves the timeslots, and says how it went', () => {
    store.save(14, [{ dayOfWeek: 'Monday', time: '19:00:00' }]);
    const save = http.expectOne({ method: 'POST', url: '/api/seasons/14/availability' });
    expect(save.request.body).toEqual({ seasonID: 14, timeslots: [{ dayOfWeek: 'Monday', time: '19:00:00' }] });
    save.flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Saved');

    store.save(14, []);
    http.expectOne('/api/seasons/14/availability').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to save');
  });
});
