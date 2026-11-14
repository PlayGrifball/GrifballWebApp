import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { SignupsTimeslotDto } from '@api/models';
import { AvailabilityService } from '../availability.service';
import { emptySignup, SignupFormStore } from './signupForm.store';

const timeslots: SignupsTimeslotDto[] = [{ optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false }];

describe('SignupFormStore', () => {
  let store: SignupFormStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    const availability = jasmine.createSpyObj<AvailabilityService>('AvailabilityService', ['getDif']);
    availability.getDif.and.returnValue(0);
    TestBed.configureTestingModule({
      providers: [
        SignupFormStore, provideHttpClient(withXhr()), provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AvailabilityService, useValue: availability },
      ],
    });
    store = TestBed.inject(SignupFormStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Shows season 5, answering its name and the events. */
  const show = (seasonID = signal(5)) => {
    store.seasonID.use(seasonID);
    TestBed.tick();
    http.expectOne(`/api/seasons/${seasonID()}/name`).flush('Winter');
    http.expectOne('/api/events').flush([]);
  };

  it('asks for nothing until it has the season, and nothing for none', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.loaded()).toBeFalse();
    store.seasonID.use(signal(0));
    TestBed.tick();
    expect(store.loaded()).toBeTrue();
    expect(store.signup()).toEqual(emptySignup());
  });

  it("loads the player's signup, with the season's name", () => {
    show();
    http.expectOne('/api/seasons/5/signups/mine?offset=0').flush({ seasonID: 5, userID: 7, teamName: 'Team', willCaptain: true, requiresAssistanceDrafting: false, timeslots });
    expect(store.loaded()).toBeTrue();
    expect(store.signup().teamName).toBe('Team');
    expect(store.signup().timeslots).toEqual(timeslots);
    expect(store.seasonName()).toBe('Winter');
  });

  it("gives an empty signup the season's timeslots before the player signs up, and says when they fail", () => {
    show();
    http.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    expect(store.loaded()).toBeFalse();
    http.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots);
    expect(store.loaded()).toBeTrue();
    expect(store.signup()).toEqual({ ...emptySignup(), timeslots });

    const seasonID = signal(6);
    show(seasonID);
    http.expectOne('/api/seasons/6/signups/mine?offset=0').flush(null);
    http.expectOne('/api/seasons/6/signups/mine/timeslots?offset=0').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeTrue();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to get timeslots');
  });

  it('says when the signup fails to load', () => {
    show();
    http.expectOne('/api/seasons/5/signups/mine?offset=0').flush('boom', { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeTrue();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to get signup');
  });

  it('saves the signup, and says how it went', () => {
    spyOn(console, 'log');
    store.submit(5, emptySignup());
    const save = http.expectOne({ method: 'POST', url: '/api/seasons/5/signups/mine' });
    expect(save.request.body).toEqual(emptySignup());
    save.flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Signup Submitted', 'OK');

    store.submit(5, emptySignup());
    http.expectOne('/api/seasons/5/signups/mine').flush({ title: 'Refused', detail: 'Signups are closed' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    expect(snackBar.open).toHaveBeenCalledWith('Error Occurred: Signups are closed', 'OK');
  });
});
