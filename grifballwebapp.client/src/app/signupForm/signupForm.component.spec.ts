import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SignupFormComponent } from './signupForm.component';
import { AvailabilityService } from '../availability.service';

describe('SignupFormComponent', () => {
  let fixture: ComponentFixture<SignupFormComponent>;
  let component: SignupFormComponent;
  let httpMock: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  const timeslots = () => [
    { id: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false, isDisabled: false, isHeader: false },
    { id: 2, dayOfWeek: 'Monday', time: '20:00:00', isChecked: true, isDisabled: false, isHeader: false },
    { id: 3, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false, isDisabled: false, isHeader: false },
  ];

  function setup(seasonID: string | null) {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    const availability = jasmine.createSpyObj<AvailabilityService>('AvailabilityService', ['getDif', 'parseTime']);
    availability.DayOfWeek = 'Day Of Week';
    availability.getDif.and.returnValue(0);
    availability.parseTime.and.callFake((t: string) => t);

    TestBed.configureTestingModule({
      imports: [SignupFormComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AvailabilityService, useValue: availability },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(seasonID === null ? {} : { seasonID }) } },
        },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SignupFormComponent);
    component = fixture.componentInstance;
  }

  afterEach(() => httpMock.verify());

  it('renders before the signup request has responded without throwing', () => {
    setup('5');
    // First change detection runs ngOnInit and binds model.timeslots while the HTTP request is still pending.
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model.timeslots).toEqual([]);
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
  });

  it('renders when there is no existing signup and the timeslots request is still pending', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    // The model is reset for a new signup; timeslots have not arrived yet.
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model.seasonID).toBe(5);
    expect(component.model.timeslots).toEqual([]);

    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row, app-availability-table tr[mat-row]');
    expect(component.model.timeslots.length).toBe(3);
    expect(rows.length).toBe(2); // Monday and Tuesday
  });

  it('renders an existing signup', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush({
      seasonID: 5, userID: 7, teamName: 'Team', willCaptain: true, requiresAssistanceDrafting: false, timeslots: timeslots(),
    });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model.teamName).toBe('Team');
    expect(component.model.timeslots.length).toBe(3);
  });

  it('does not throw if the API response omits timeslots', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush({ seasonID: 5, userID: 7, willCaptain: false });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model.timeslots).toEqual([]);
  });

  it('does not throw and shows a snackbar when the signup request fails', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush('boom', { status: 500, statusText: 'Server Error' });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to get signup');
  });

  it('renders without a season ID and makes no request', () => {
    setup(null);
    expect(() => fixture.detectChanges()).not.toThrow();
    httpMock.expectNone(() => true);
  });
});
