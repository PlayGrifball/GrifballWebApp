import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Location } from '@angular/common';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RescheduleRequestComponent } from './reschedule-request.component';

describe('RescheduleRequestComponent', () => {
  let fixture: ComponentFixture<RescheduleRequestComponent>;
  let component: RescheduleRequestComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let location: jasmine.SpyObj<Location>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    location = jasmine.createSpyObj('Location', ['back']);
    await TestBed.configureTestingModule({
      imports: [RescheduleRequestComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: Location, useValue: location },
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(RescheduleRequestComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonMatchID', 31);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const buttons = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.rr-actions button')) as HTMLButtonElement[];
  const submitButton = () => buttons().find(b => b.textContent!.includes('Submit'))!;

  it('reads the season match from the route', () => {
    expect(component.seasonMatchID()).toBe(31);
  });

  it('keeps submit disabled until a non-blank reason is entered', async () => {
    const typeReason = async (text: string) => {
      const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea')!;
      textarea.value = text;
      textarea.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    expect(submitButton().disabled).toBeTrue();

    await typeReason('   ');
    expect(submitButton().disabled).toBeTrue();

    await typeReason('Family emergency');
    expect(component.reason).toBe('Family emergency');
    expect(submitButton().disabled).toBeFalse();
  });

  it('refuses to submit a blank reason', () => {
    component.reason = '  ';
    component.submitRescheduleRequest();

    expect(snackBar.open).toHaveBeenCalledWith('Please provide a reason for the reschedule', 'Close', { duration: 3000 });
    expect(component.submitting()).toBeFalse();
    http.expectNone('/api/season-matches/31/reschedules');
  });

  it('sends a trimmed reason without a time when no new time is proposed', () => {
    component.reason = '  Team conflict  ';
    component.submitRescheduleRequest();

    const req = http.expectOne('/api/season-matches/31/reschedules');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ seasonMatchID: 31, newScheduledTime: undefined, reason: 'Team conflict' });
    req.flush({});
  });

  it('clears the form and requests for the new match when the route moves to another', () => {
    component.reason = 'Team conflict';
    component.newDate = new Date(2026, 2, 4);
    component.newTime = '19:30';

    fixture.componentRef.setInput('seasonMatchID', 32);
    fixture.detectChanges();
    expect(component.reason).toBe('');
    expect(component.newDate).toBeUndefined();
    expect(component.newTime).toBe('');

    component.reason = 'Travel';
    component.submitRescheduleRequest();
    expect(http.expectOne('/api/season-matches/32/reschedules').request.body.seasonMatchID).toBe(32);
  });

  it('ignores a date without a time', () => {
    component.reason = 'x';
    component.newDate = new Date(2026, 2, 4);
    component.submitRescheduleRequest();

    expect(http.expectOne('/api/season-matches/31/reschedules').request.body.newScheduledTime).toBeUndefined();
  });

  it('combines the chosen date and time into an ISO timestamp', () => {
    component.reason = 'x';
    component.newDate = new Date(2026, 2, 4);
    component.newTime = '19:30';
    component.submitRescheduleRequest();

    const body = http.expectOne('/api/season-matches/31/reschedules').request.body;
    expect(body.newScheduledTime).toBe(new Date(2026, 2, 4, 19, 30, 0, 0).toISOString());
  });

  it('shows "Submitting..." while in flight, then confirms and goes back', () => {
    component.reason = 'x';
    component.submitRescheduleRequest();
    fixture.detectChanges();
    expect(component.submitting()).toBeTrue();
    expect(submitButton().textContent).toContain('Submitting...');
    expect(submitButton().disabled).toBeTrue();

    http.expectOne('/api/season-matches/31/reschedules').flush({});

    expect(snackBar.open).toHaveBeenCalledWith('Reschedule request submitted successfully!', 'Close', { duration: 5000 });
    expect(location.back).toHaveBeenCalled();
    expect(component.submitting()).toBeFalse();
  });

  it('reports a failure and stays on the page', () => {
    spyOn(console, 'error');
    component.reason = 'x';
    component.submitRescheduleRequest();
    http.expectOne('/api/season-matches/31/reschedules').flush(null, { status: 500, statusText: 'Server Error' });

    expect(snackBar.open).toHaveBeenCalledWith('Failed to submit reschedule request. Please try again.', 'Close', { duration: 5000 });
    expect(location.back).not.toHaveBeenCalled();
    expect(component.submitting()).toBeFalse();
  });

  it("shows the server's reason for a refusal", () => {
    spyOn(console, 'error');
    component.reason = 'x';
    component.submitRescheduleRequest();
    http.expectOne('/api/season-matches/31/reschedules').flush(
      { title: 'Conflict', status: 409, detail: 'There is already an active reschedule request for this match.' },
      { status: 409, statusText: 'Conflict' });

    expect(snackBar.open).toHaveBeenCalledWith('There is already an active reschedule request for this match.', 'Close', { duration: 5000 });
    expect(location.back).not.toHaveBeenCalled();
  });

  it('goes back when cancel is clicked', () => {
    buttons().find(b => b.textContent!.includes('Cancel'))!.click();
    expect(location.back).toHaveBeenCalled();
  });
});
