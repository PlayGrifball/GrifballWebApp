import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SignupFormComponent } from './signupForm.component';
import { AvailabilityService } from '../availability.service';
import { AccountService } from '../account.service';
import { signal } from '@angular/core';

describe('SignupFormComponent', () => {
  let fixture: ComponentFixture<SignupFormComponent>;
  let component: SignupFormComponent;
  let httpMock: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  // Exactly what GET /api/Signups/getTimeslots returns (GrifballWebApp.Server.Signups.TimeslotDto).
  const timeslots = () => [
    { optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
    { optionID: 42, dayOfWeek: 'Monday', time: '20:00:00', isChecked: true },
    { optionID: 57, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false },
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
        { provide: AccountService, useValue: { displayName: signal('Grif') } },
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

  /** The season name and dates for the heading and timeline; most tests do not look at them. */
  const seasonInfo = () => httpMock.match(r => r.url === '/api/Home/CurrentAndFutureEvents/' || r.url.startsWith('/api/Season/getSeasonName/'));

  afterEach(() => {
    seasonInfo();
    httpMock.verify();
  });

  it('renders before the signup request has responded without throwing', () => {
    setup('5');
    // First change detection runs ngOnInit and binds model.timeslots while the HTTP request is still pending.
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().timeslots).toEqual([]);
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
  });

  it('renders when there is no existing signup and the timeslots request is still pending', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    // The model is reset for a new signup; timeslots have not arrived yet.
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().seasonID).toBe(5);
    expect(component.model().timeslots).toEqual([]);

    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row, app-availability-table tr[mat-row]');
    expect(component.model().timeslots.length).toBe(3);
    expect(rows.length).toBe(2); // Monday and Tuesday
  });

  it('renders an existing signup', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush({
      seasonID: 5, userID: 7, teamName: 'Team', willCaptain: true, requiresAssistanceDrafting: false, timeslots: timeslots(),
    });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().teamName).toBe('Team');
    expect(component.model().timeslots.length).toBe(3);
  });

  it('does not throw if the API response omits timeslots', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush({ seasonID: 5, userID: 7, willCaptain: false });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().timeslots).toEqual([]);
  });

  it('does not throw and shows a snackbar when the signup request fails', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush('boom', { status: 500, statusText: 'Server Error' });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to get signup');
  });

  it("shows who is signing up, the season's name and its timeline", () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
    httpMock.expectOne('/api/Season/getSeasonName/5').flush('Winter');
    const now = Date.parse('2026-03-10T12:00:00Z');
    component.now.set(now);
    httpMock.expectOne('/api/Home/CurrentAndFutureEvents/').flush([
      { seasonID: 5, name: 'Winter', start: '2026-04-01T00:00:00Z', end: '2026-06-01T00:00:00Z', eventType: 'Season' },
      { seasonID: 9, name: 'Other', start: '2026-03-01T00:00:00Z', end: '2026-03-20T00:00:00Z', eventType: 'Signup' },
      { seasonID: 5, name: 'Winter', start: '2026-03-01T00:00:00Z', end: '2026-03-20T00:00:00Z', eventType: 'Signup' },
      { seasonID: 5, name: 'Winter', start: '2026-03-25T19:00:00Z', end: '2026-03-25T23:00:00Z', eventType: 'Draft' },
    ]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.hud-kicker')!.textContent).toContain('Winter');
    expect(el.querySelector('.enlist-who__name')!.textContent).toBe('Grif');
    expect(component.timeline().map(e => [e.eventType, e.active])).toEqual([['Signup', true], ['Draft', false], ['Season', false]]);
    const steps = Array.from(el.querySelectorAll('.enlist-step')).map(s => s.textContent!.replace(/\s+/g, ' ').trim());
    expect(steps.length).toBe(3);
    expect(steps[0]).toMatch(/^Now · until .* Signups$/);
    expect(steps[2]).toContain('→');
    expect(el.querySelector('.enlist-step--now')).not.toBeNull();
    expect(el.querySelector('a.hud-more')!.getAttribute('href')).toBe('/season/5/signups');
  });

  it('keeps the page hidden until the signup and its timeslots are in', () => {
    setup('5');
    fixture.detectChanges();
    expect(component.loaded()).toBeFalse();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    expect(component.loaded()).toBeFalse();
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush('x', { status: 500, statusText: 'Server Error' });
    expect(component.loaded()).toBeTrue();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.hud-page')!.classList).not.toContain('hud-waiting');
  });

  it('shows the page at once without a season, and after an existing signup or a failure', () => {
    setup(null);
    fixture.detectChanges();
    expect(component.loaded()).toBeTrue();
  });

  it('still works when the season name and dates cannot be loaded', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
    httpMock.expectOne('/api/Season/getSeasonName/5').flush('x', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/Home/CurrentAndFutureEvents/').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.seasonName()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No upcoming dates for this season.');
  });

  it('renders without a season ID and makes no request', () => {
    setup(null);
    expect(() => fixture.detectChanges()).not.toThrow();
    httpMock.expectNone(() => true);
  });

  it('renders the timeslots once they arrive, without a manual detectChanges', async () => {
    setup('5');
    await fixture.whenStable();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row').length).toBe(2);
  });

  it('submits the slots ticked in the availability table, in the API time format', async () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
    await fixture.whenStable();

    // Tuesday 19:00 is the only Tuesday slot; its checkbox is the first one in that row.
    const tuesday = fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row')[1] as HTMLElement;
    tuesday.querySelector<HTMLInputElement>('mat-checkbox input')!.click();
    await fixture.whenStable();

    (component as any).signupForm = () => ({ valid: true });
    component.onSubmit();
    const body = httpMock.expectOne('/api/Signups/UpsertSignup/').request.body;
    expect(body.timeslots).toEqual([
      { optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
      { optionID: 42, dayOfWeek: 'Monday', time: '20:00:00', isChecked: true },
      { optionID: 57, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: true },
    ]);
  });

  it('replaces the model on form edits, and ticking one slot ticks only that slot', async () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/Signups/getSignup/5?offset=0').flush(null);
    httpMock.expectOne('/api/Signups/getTimeslots/5?offset=0').flush(timeslots());
    await fixture.whenStable();
    const loaded = component.model();

    fixture.nativeElement.querySelector('mat-checkbox[name="willCaptain"] input').click();
    await fixture.whenStable();
    const team = fixture.nativeElement.querySelector('input[name="teamName"]') as HTMLInputElement;
    team.value = 'Reds';
    team.dispatchEvent(new Event('input'));
    const monday = fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row')[0] as HTMLElement;
    monday.querySelectorAll<HTMLInputElement>('mat-checkbox input')[0].click(); // Monday 19:00
    await fixture.whenStable();

    expect(component.model()).not.toBe(loaded);
    expect(loaded.willCaptain).toBeFalse();
    expect(loaded.timeslots.map(t => t.isChecked)).toEqual([false, true, false]);
    expect(component.model().willCaptain).toBeTrue();
    expect(component.model().teamName).toBe('Reds');
    expect(component.model().timeslots.map(t => [t.optionID, t.isChecked])).toEqual([[41, true], [42, true], [57, false]]);
  });

  describe('onSubmit', () => {
    // signupForm is a viewChild() signal; swap it for one that returns a stand-in form.
    const stubForm = (valid: boolean) => (component as any).signupForm = () => ({ valid });

    beforeEach(() => {
      setup(null);
      fixture.detectChanges();
    });

    it('does not submit an invalid form', () => {
      stubForm(false);
      component.onSubmit();
      httpMock.expectNone('/api/Signups/UpsertSignup/');
    });

    it('submits the signup and confirms it', () => {
      stubForm(true);
      component.model.update(model => ({ ...model, seasonID: 5, teamName: 'Reds' }));
      component.onSubmit();

      const req = httpMock.expectOne('/api/Signups/UpsertSignup/');
      expect(req.request.body).toEqual(jasmine.objectContaining({ seasonID: 5, teamName: 'Reds' }));
      req.flush({});
      expect(snackBar.open).toHaveBeenCalledWith('Signup Submitted', 'OK');
    });

    it('reports a failed submission', () => {
      stubForm(true);
      spyOn(console, 'log');
      component.onSubmit();

      httpMock.expectOne('/api/Signups/UpsertSignup/').flush('Signups are closed', { status: 400, statusText: 'Bad Request' });
      expect(snackBar.open).toHaveBeenCalledWith('Error Occurred: Signups are closed', 'OK');
    });
  });
});
