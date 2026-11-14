import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SignupFormComponent } from './signupForm.component';
import { AvailabilityService } from '../availability.service';
import { AccountService } from '@core/auth/account.service';
import { signal } from '@angular/core';
import { noMaterialAnimations } from '@shared/testing/noMaterialAnimations';

describe('SignupFormComponent', () => {
  let fixture: ComponentFixture<SignupFormComponent>;
  let component: SignupFormComponent;
  let httpMock: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  // Exactly what GET /api/seasons/{seasonID}/signups/mine/timeslots returns (GrifballWebApp.Server.Signups.TimeslotDto).
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
      imports: [SignupFormComponent],
      providers: [
        noMaterialAnimations,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AvailabilityService, useValue: availability },
        { provide: AccountService, useValue: { displayName: signal('Grif') } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SignupFormComponent);
    component = fixture.componentInstance;
    // As the router gives it: the :seasonID of the route, as text
    fixture.componentRef.setInput('seasonID', seasonID);
  }

  /** The season name and dates for the heading and timeline; most tests do not look at them. */
  const seasonInfo = () => httpMock.match(r => r.url === '/api/events' || /^\/api\/seasons\/\d+\/name$/.test(r.url));

  afterEach(() => {
    seasonInfo();
    httpMock.verify();
  });

  it('renders before the signup request has responded without throwing', () => {
    setup('5');
    // First change detection runs ngOnInit and binds model.timeslots while the HTTP request is still pending.
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().timeslots).toEqual([]);
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots());
  });

  it('renders when there is no existing signup and the timeslots request is still pending', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    // The model is reset for a new signup; timeslots have not arrived yet.
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().timeslots).toEqual([]);

    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots());
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row, app-availability-table tr[mat-row]');
    expect(component.model().timeslots).toHaveSize(3);
    expect(rows).toHaveSize(2); // Monday and Tuesday
  });

  it('renders an existing signup', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush({
      seasonID: 5, userID: 7, teamName: 'Team', willCaptain: true, requiresAssistanceDrafting: false, timeslots: timeslots(),
    });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().teamName).toBe('Team');
    expect(component.model().timeslots).toHaveSize(3);
  });

  it('does not throw if the API response omits timeslots', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush({ seasonID: 5, userID: 7, willCaptain: false });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.model().timeslots).toEqual([]);
  });

  it('does not throw and shows a snackbar when the signup request fails', () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush('boom', { status: 500, statusText: 'Server Error' });
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to get signup');
  });

  it("shows who is signing up, the season's name and its timeline", () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots());
    httpMock.expectOne('/api/seasons/5/name').flush('Winter');
    const now = Date.parse('2026-03-10T12:00:00Z');
    component.now.set(now);
    httpMock.expectOne('/api/events').flush([
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
    expect(steps).toHaveSize(3);
    expect(steps[0]).toMatch(/^Now · until .* Signups$/);
    expect(steps[2]).toContain('→');
    expect(el.querySelector('.enlist-step--now')).not.toBeNull();
    expect(el.querySelector('a.hud-more')!.getAttribute('href')).toBe('/season/5/signups');
  });

  it('keeps the page hidden until the signup and its timeslots are in', () => {
    setup('5');
    fixture.detectChanges();
    expect(component.loaded()).toBeFalse();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    expect(component.loaded()).toBeFalse();
    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush('x', { status: 500, statusText: 'Server Error' });
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
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots());
    httpMock.expectOne('/api/seasons/5/name').flush('x', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/events').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.seasonName()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No upcoming dates for this season.');
  });

  it("resets the form to the new season's signup when the route moves to another season", () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush({
      seasonID: 5, userID: 7, teamName: 'Reds', willCaptain: true, requiresAssistanceDrafting: false, timeslots: timeslots(),
    });
    httpMock.expectOne('/api/seasons/5/name').flush('Winter');
    fixture.detectChanges();
    expect(component.model().teamName).toBe('Reds');

    fixture.componentRef.setInput('seasonID', '6');
    fixture.detectChanges();
    // Nothing of season 5's signup is left in the form while season 6's loads
    expect(component.model().teamName).not.toBe('Reds');
    expect(component.model().timeslots).toEqual([]);
    expect(component.seasonName()).toBeNull();
    expect(component.loaded()).toBeFalse();

    httpMock.expectOne('/api/seasons/6/signups/mine?offset=0').flush(null);
    httpMock.expectOne('/api/seasons/6/signups/mine/timeslots?offset=0').flush(timeslots());
    httpMock.expectOne('/api/seasons/6/name').flush('Spring');
    fixture.detectChanges();

    expect(component.seasonID()).toBe(6);
    expect(component.model().willCaptain).toBeFalsy();
    expect(component.model().timeslots).toHaveSize(3);
    expect(component.seasonName()).toBe('Spring');
    expect(component.loaded()).toBeTrue();
  });

  it("drops the previous season's signup and name when they arrive late", () => {
    setup('5');
    fixture.detectChanges();
    const lateSignup = httpMock.expectOne('/api/seasons/5/signups/mine?offset=0');
    const lateName = httpMock.expectOne('/api/seasons/5/name');

    fixture.componentRef.setInput('seasonID', '6');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/6/signups/mine?offset=0').flush({
      seasonID: 6, userID: 7, teamName: 'Blues', willCaptain: false, requiresAssistanceDrafting: false, timeslots: timeslots(),
    });
    httpMock.expectOne('/api/seasons/6/name').flush('Spring');
    lateSignup.flush({ seasonID: 5, userID: 7, teamName: 'Reds', willCaptain: true, requiresAssistanceDrafting: false, timeslots: [] });
    lateName.flush('Winter');

    expect(component.model().teamName).toBe('Blues');
    expect(component.seasonName()).toBe('Spring');
  });

  it("drops the previous season's timeslots when they arrive late", () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    const lateTimeslots = httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0');

    fixture.componentRef.setInput('seasonID', '6');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/6/signups/mine?offset=0').flush(null);
    lateTimeslots.flush(timeslots());

    expect(component.model().timeslots).toEqual([]);
    expect(component.loaded()).toBeFalse();
    httpMock.expectOne('/api/seasons/6/signups/mine/timeslots?offset=0').flush([]);
  });

  it('renders without a season ID and makes no request', () => {
    setup(null);
    expect(() => fixture.detectChanges()).not.toThrow();
    httpMock.expectNone(() => true);
  });

  it('renders the timeslots once they arrive, without a manual detectChanges', async () => {
    setup('5');
    await fixture.whenStable();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots());
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row')).toHaveSize(2);
  });

  it('submits the slots ticked in the availability table, in the API time format', async () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots());
    await fixture.whenStable();

    // Tuesday 19:00 is the only Tuesday slot; its checkbox is the first one in that row.
    const tuesday = fixture.nativeElement.querySelectorAll('app-availability-table tr.mat-mdc-row')[1] as HTMLElement;
    tuesday.querySelector<HTMLInputElement>('mat-checkbox input')!.click();
    await fixture.whenStable();

    (component as unknown as { signupForm: () => unknown }).signupForm = () => ({ valid: true });
    component.onSubmit();
    const body = httpMock.expectOne('/api/seasons/5/signups/mine').request.body;
    expect(body.timeslots).toEqual([
      { optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
      { optionID: 42, dayOfWeek: 'Monday', time: '20:00:00', isChecked: true },
      { optionID: 57, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: true },
    ]);
  });

  it('replaces the model on form edits, and ticking one slot ticks only that slot', async () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush(null);
    httpMock.expectOne('/api/seasons/5/signups/mine/timeslots?offset=0').flush(timeslots());
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

  it('caps the team name at 30 characters, and an old longer one shows an error and is not submitted', async () => {
    setup('5');
    fixture.detectChanges();
    httpMock.expectOne('/api/seasons/5/signups/mine?offset=0').flush({
      seasonID: 5, userID: 7, teamName: 'x'.repeat(31), willCaptain: true, requiresAssistanceDrafting: false, timeslots: timeslots(),
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input[name="teamName"]') as HTMLInputElement;
    expect(input.getAttribute('maxlength')).toBe('30');
    const control = component.signupForm().controls['teamName'];
    expect(control.errors?.['maxlength']).toEqual(jasmine.objectContaining({ requiredLength: 30 }));
    control.markAsTouched();
    fixture.detectChanges();
    expect(input.closest('mat-form-field')!.textContent).toContain('Maximum length 30');

    component.onSubmit();
    httpMock.expectNone('/api/seasons/5/signups/mine');

    input.value = 'x'.repeat(30);
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(control.errors?.['maxlength']).toBeUndefined();
  });

  describe('onSubmit', () => {
    // signupForm is a viewChild() signal; swap it for one that returns a stand-in form.
    const stubForm = (valid: boolean) => (component as unknown as { signupForm: () => unknown }).signupForm = () => ({ valid });
    /** The page on season 5 (the route's), its loads answered: no signup yet. */
    const onSeason5 = () => {
      fixture.componentRef.setInput('seasonID', 5);
      fixture.detectChanges();
      for (let round = 0; round < 2; round++)
        httpMock.match(r => r.method === 'GET').forEach(r => r.flush(null));
    };

    beforeEach(() => {
      setup(null);
      fixture.detectChanges();
    });

    it('does not submit an invalid form', () => {
      stubForm(false);
      component.onSubmit();
      expect(httpMock.match('/api/seasons/5/signups/mine')).withContext('signups sent').toEqual([]);
    });

    it('submits the signup and confirms it', () => {
      stubForm(true);
      onSeason5();
      component.model.update(model => ({ ...model, teamName: 'Reds' }));
      component.onSubmit();

      const req = httpMock.expectOne('/api/seasons/5/signups/mine');
      expect(req.request.body).toEqual(jasmine.objectContaining({ teamName: 'Reds' }));
      // Whose and which season are the server's: the route's, the signed-in player
      expect(req.request.body.seasonID).toBeUndefined();
      req.flush({});
      expect(snackBar.open).toHaveBeenCalledWith('Signup Submitted', 'OK');
    });

    it('reports a failed submission', () => {
      stubForm(true);
      onSeason5();
      spyOn(console, 'log');
      component.onSubmit();

      httpMock.expectOne('/api/seasons/5/signups/mine').flush({ status: 400, title: 'Request refused', detail: 'Signups are closed' }, { status: 400, statusText: 'Bad Request' });
      expect(snackBar.open).toHaveBeenCalledWith('Error Occurred: Signups are closed', 'OK');
    });
  });
});
