import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ProcessRescheduleDialogComponent } from './process-reschedule-dialog.component';
import type { RescheduleDto } from '@api/models';

describe('ProcessRescheduleDialogComponent', () => {
  let fixture: ComponentFixture<ProcessRescheduleDialogComponent>;
  let component: ProcessRescheduleDialogComponent;
  let http: HttpTestingController;
  let dialogRef: jasmine.SpyObj<MatDialogRef<ProcessRescheduleDialogComponent>>;

  const reschedule: RescheduleDto = {
    matchRescheduleID: 55,
    seasonMatchID: 9,
    homeCaptain: 'HomeCap',
    awayCaptain: 'AwayCap',
    originalScheduledTime: '2026-03-01T19:00:00Z',
    newScheduledTime: null,
    reason: 'Out of town',
    requestedByGamertag: 'Requester',
    requestedByUserID: null,
    requestedAt: '2026-02-20T12:00:00Z',
    status: 'Pending',
    discordThreadID: null,
    canApprove: true,
    whyNotApprovable: null,
  };

  beforeEach(async () => {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    await TestBed.configureTestingModule({
      imports: [ProcessRescheduleDialogComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: reschedule },
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProcessRescheduleDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const actionButton = () => el().querySelectorAll('mat-dialog-actions button')[1] as HTMLButtonElement;

  it('shows the request details, with "Not scheduled" for a missing time', () => {
    const text = el().textContent!;
    expect(text).toContain('HomeCap vs AwayCap');
    expect(text).toContain('Out of town');
    expect(text).toContain('Requester');
    expect(text).toContain(new Date('2026-03-01T19:00:00Z').toLocaleString());
    expect(text).toContain('Not scheduled');
  });

  it('formats dates in the local format', () => {
    expect(component.formatDateTime()).toBe('Not scheduled');
    expect(component.formatDateTime('')).toBe('Not scheduled');
    expect(component.formatDateTime('2026-02-20T12:00:00Z')).toBe(new Date('2026-02-20T12:00:00Z').toLocaleString());
  });

  it('disables the action until a decision is chosen, then labels it accordingly', () => {
    expect(actionButton().disabled).toBeTrue();

    component.decision.set('approve');
    fixture.detectChanges();
    expect(actionButton().disabled).toBeFalse();
    expect(actionButton().textContent).toContain('Approve');

    component.decision.set('reject');
    fixture.detectChanges();
    expect(actionButton().textContent).toContain('Reject');
  });

  it('does nothing without a decision', () => {
    component.processReschedule();
    http.expectNone(() => true);
    expect(component.processing()).toBeFalse();
  });

  it('approves with trimmed notes and closes with true', () => {
    component.decision.set('approve');
    component.commissionerNotes.set('  Fine by me  ');
    component.processReschedule();
    fixture.detectChanges();
    expect(actionButton().textContent).toContain('Processing...');

    const req = http.expectOne('/api/reschedules/55/decision');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ approved: true, commissionerNotes: 'Fine by me' });
    req.flush({});

    expect(dialogRef.close).toHaveBeenCalledWith(true);
    expect(component.processing()).toBeFalse();
  });

  it('rejects without notes when the notes are blank', () => {
    component.decision.set('reject');
    component.commissionerNotes.set('   ');
    component.processReschedule();

    const req = http.expectOne('/api/reschedules/55/decision');
    expect(req.request.body).toEqual({ approved: false, commissionerNotes: undefined });
    req.flush({});
  });

  it('stays open and re-enables the action when processing fails', () => {
    spyOn(console, 'error');
    component.decision.set('approve');
    component.processReschedule();
    http.expectOne('/api/reschedules/55/decision').flush('x', { status: 500, statusText: 'Server Error' });

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(component.processing()).toBeFalse();
  });

  it("shows the server's reason when it refuses, as an alert, and stays usable", async () => {
    component.decision.set('approve');
    component.processReschedule();
    http.expectOne('/api/reschedules/55/decision')
      .flush({ status: 400, title: 'Request refused', detail: 'This match already has a result.' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    const alert = el().querySelector('[role="alert"]')!;
    expect(alert.textContent).toContain('This match already has a result.');
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(actionButton().disabled).toBeFalse();

    // A second try clears the old error; this time it goes through
    component.decision.set('reject');
    component.processReschedule();
    await fixture.whenStable();
    expect(el().querySelector('[role="alert"]')).toBeNull();
    http.expectOne('/api/reschedules/55/decision').flush({});
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('shows a general message when the server gives no reason', async () => {
    component.decision.set('approve');
    component.processReschedule();
    http.expectOne('/api/reschedules/55/decision').flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect(el().querySelector('[role="alert"]')!.textContent).toContain('The request could not be processed. Try again.');
  });

  it('closes with false on cancel', () => {
    (el().querySelector('mat-dialog-actions button') as HTMLButtonElement).click();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });

  it('re-enables the buttons after a failed request, without a manual detectChanges', async () => {
    spyOn(console, 'error');
    await fixture.whenStable();
    component.decision.set('approve');
    component.processReschedule();
    await fixture.whenStable();
    expect(actionButton().textContent).toContain('Processing...');

    http.expectOne('/api/reschedules/55/decision').flush('x', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect(actionButton().textContent).toContain('Approve');
    expect(actionButton().disabled).toBeFalse();
  });
});

describe('ProcessRescheduleDialogComponent for a match that can no longer move', () => {
  let fixture: ComponentFixture<ProcessRescheduleDialogComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    const reschedule: RescheduleDto = {
      matchRescheduleID: 56, seasonMatchID: 9, homeCaptain: 'HomeCap', awayCaptain: 'AwayCap', reason: 'Out of town',
      originalScheduledTime: null, newScheduledTime: null, discordThreadID: null,
      requestedByGamertag: 'Requester', requestedByUserID: null, requestedAt: '2026-02-20T12:00:00Z', status: 'Pending',
      canApprove: false, whyNotApprovable: 'This match already has a result.',
    };
    await TestBed.configureTestingModule({
      imports: [ProcessRescheduleDialogComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MatDialogRef, useValue: jasmine.createSpyObj('MatDialogRef', ['close']) },
        { provide: MAT_DIALOG_DATA, useValue: reschedule },
      ]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProcessRescheduleDialogComponent);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('offers only Reject, chosen already, and says why', async () => {
    const el = fixture.nativeElement as HTMLElement;
    const options = Array.from(el.querySelectorAll('mat-radio-button')).map(r => r.textContent!.trim());
    expect(options).toEqual(['Reject reschedule']);
    expect(el.querySelector('.prd-reject-only')!.textContent).toContain('This match already has a result.');
    const action = el.querySelectorAll<HTMLButtonElement>('mat-dialog-actions button')[1];
    expect(action.disabled).toBeFalse();
    expect(action.textContent).toContain('Reject');

    action.click();
    expect(http.expectOne('/api/reschedules/56/decision').request.body).toEqual({ approved: false, commissionerNotes: undefined });
  });
});
