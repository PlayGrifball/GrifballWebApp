import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ProcessRescheduleDialogComponent } from './process-reschedule-dialog.component';
import { RescheduleDto } from '../commissioner-dashboard.component';

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
    newScheduledTime: undefined,
    reason: 'Out of town',
    requestedByGamertag: 'Requester',
    requestedAt: '2026-02-20T12:00:00Z',
    status: 0,
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
    expect(component.formatDateTime(undefined)).toBe('Not scheduled');
    expect(component.formatDateTime('')).toBe('Not scheduled');
    expect(component.formatDateTime('2026-02-20T12:00:00Z')).toBe(new Date('2026-02-20T12:00:00Z').toLocaleString());
  });

  it('disables the action until a decision is chosen, then labels it accordingly', () => {
    expect(actionButton().disabled).toBeTrue();

    component.decision = 'approve';
    fixture.detectChanges();
    expect(actionButton().disabled).toBeFalse();
    expect(actionButton().textContent).toContain('Approve');

    component.decision = 'reject';
    fixture.detectChanges();
    expect(actionButton().textContent).toContain('Reject');
  });

  it('does nothing without a decision', async () => {
    await component.processReschedule();
    http.expectNone(() => true);
    expect(component.processing).toBeFalse();
  });

  it('approves with trimmed notes and closes with true', async () => {
    component.decision = 'approve';
    component.commissionerNotes = '  Fine by me  ';
    await component.processReschedule();
    fixture.detectChanges();
    expect(actionButton().textContent).toContain('Processing...');

    const req = http.expectOne('/api/Reschedule/ProcessReschedule/55');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ approved: true, commissionerNotes: 'Fine by me' });
    req.flush({});

    expect(dialogRef.close).toHaveBeenCalledWith(true);
    expect(component.processing).toBeFalse();
  });

  it('rejects without notes when the notes are blank', async () => {
    component.decision = 'reject';
    component.commissionerNotes = '   ';
    await component.processReschedule();

    const req = http.expectOne('/api/Reschedule/ProcessReschedule/55');
    expect(req.request.body).toEqual({ approved: false, commissionerNotes: undefined });
    req.flush({});
  });

  it('stays open and re-enables the action when processing fails', async () => {
    spyOn(console, 'error');
    component.decision = 'approve';
    await component.processReschedule();
    http.expectOne('/api/Reschedule/ProcessReschedule/55').flush('x', { status: 500, statusText: 'Server Error' });

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(component.processing).toBeFalse();
  });

  it('closes with false on cancel', () => {
    (el().querySelector('mat-dialog-actions button') as HTMLButtonElement).click();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });
});
