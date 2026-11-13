import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';

import { CommissionerDashboardComponent, CommissionerDashboardDto, RescheduleDto } from './commissionerDashboard.component';
import { noMaterialAnimations } from '@shared/testing/noMaterialAnimations';

describe('CommissionerDashboardComponent', () => {
  let component: CommissionerDashboardComponent;
  let fixture: ComponentFixture<CommissionerDashboardComponent>;
  let httpTestingController: HttpTestingController;
  let mockDialog: jasmine.SpyObj<MatDialog>;
  let mockDialogRef: jasmine.SpyObj<MatDialogRef<any>>;

  beforeEach(async () => {
    mockDialogRef = jasmine.createSpyObj('MatDialogRef', ['afterClosed']);
    mockDialogRef.afterClosed.and.returnValue(of(null));
    
    // Create a proper mock with _openedDialogs property
    mockDialog = jasmine.createSpyObj('MatDialog', ['open']);
    mockDialog.open.and.returnValue(mockDialogRef);
    Object.defineProperty(mockDialog, '_openedDialogs', {
      value: [],
      writable: true
    });

    await TestBed.configureTestingModule({
      imports: [
        CommissionerDashboardComponent,
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        noMaterialAnimations,
        { provide: MatDialog, useValue: mockDialog },
        provideRouter([]),
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CommissionerDashboardComponent);
    component = fixture.componentInstance;
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with loading state', () => {
    expect(component.loading()).toBeTruthy();
    expect(component.dashboardData()).toBeNull();
    expect(component.error()).toBeNull();
  });

  it('should have correct displayed columns for tables', () => {
    expect(component.rescheduleDisplayedColumns).toEqual([
      'match', 'originalTime', 'newTime', 'requestedBy', 'reason', 'requestedAt', 'process', 'thread'
    ]);
    expect(component.overdueDisplayedColumns).toEqual([
      'match', 'scheduledTime', 'hoursOverdue', 'severity'
    ]);
  });

  it('should load dashboard data on init', () => {
    const mockDashboardData: CommissionerDashboardDto = {
      summary: {
        pendingRescheduleCount: 2,
        overdueMatchCount: 1,
        criticalOverdueCount: 0
      },
      pendingReschedules: [
        {
          matchRescheduleID: 1,
          seasonMatchID: 101,
          homeCaptain: 'Captain1',
          awayCaptain: 'Captain2',
          originalScheduledTime: '2024-01-15T20:00:00Z',
          newScheduledTime: '2024-01-16T21:00:00Z',
          requestedByGamertag: 'Player1',
          requestedByUserID: null,
          reason: 'Schedule conflict',
          requestedAt: '2024-01-14T10:00:00Z',
          status: 'Pending',
          discordThreadID: null,
          canApprove: true,
          whyNotApprovable: null,
        }
      ],
      overdueMatches: [
        {
          seasonMatchID: 201,
          homeCaptain: 'Captain3',
          awayCaptain: 'Captain4',
          scheduledTime: '2024-01-10T19:00:00Z',
          hoursOverdue: 48
        }
      ]
    };

    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/commissioner-dashboard');
    expect(req.request.method).toBe('GET');
    req.flush(mockDashboardData);

    expect(component.loading()).toBeFalsy();
    expect(component.dashboardData()).toEqual(mockDashboardData);
    expect(component.error()).toBeNull();
  });

  it('should handle error when loading dashboard data', () => {
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/commissioner-dashboard');
    req.flush('Error loading data', { status: 500, statusText: 'Internal Server Error' });

    expect(component.loading()).toBeFalsy();
    expect(component.dashboardData()).toBeNull();
    expect(component.error()).toBe('Failed to load dashboard data');
  });

  it('should format datetime correctly', () => {
    const testDate = '2024-01-15T20:30:00Z';
    const result = component.formatDateTime(testDate);
    
    // The exact format depends on the DateTime implementation,
    // but we can check that it returns a non-empty string
    expect(result).toBeTruthy();
    expect(typeof result).toBe('string');
  });

  it('should handle undefined datetime', () => {
    const result = component.formatDateTime();
    expect(result).toBe('Not scheduled');
  });

  it('should get correct overdue severity levels', () => {
    expect(component.getOverdueSeverity(12)).toBe('normal');
    expect(component.getOverdueSeverity(30)).toBe('normal');
    expect(component.getOverdueSeverity(50)).toBe('warning');
    expect(component.getOverdueSeverity(80)).toBe('critical');
  });

  it('should get correct overdue severity colors', () => {
    expect(component.getOverdueSeverityColor(12)).toBe('primary');
    expect(component.getOverdueSeverityColor(30)).toBe('primary');
    expect(component.getOverdueSeverityColor(50)).toBe('accent');
    expect(component.getOverdueSeverityColor(80)).toBe('warn');
  });

  it('should open process reschedule dialog', () => {
    const mockReschedule: RescheduleDto = {
      matchRescheduleID: 1,
      seasonMatchID: 101,
      homeCaptain: 'Captain1',
      awayCaptain: 'Captain2',
      originalScheduledTime: '2024-01-15T20:00:00Z',
      newScheduledTime: '2024-01-16T21:00:00Z',
      requestedByGamertag: 'Player1',
      requestedByUserID: null,
      reason: 'Schedule conflict',
      requestedAt: '2024-01-14T10:00:00Z',
      status: 'Pending',
      discordThreadID: null,
      canApprove: true,
      whyNotApprovable: null,
    };

    // Spy on the dialog's open method directly
    spyOn(component['dialog'], 'open').and.returnValue(mockDialogRef);

    component.openProcessRescheduleDialog(mockReschedule);

    expect(component['dialog'].open).toHaveBeenCalled();
  });

  it('should create discord thread', () => {
    const mockReschedule: RescheduleDto = {
      matchRescheduleID: 1,
      seasonMatchID: 101,
      homeCaptain: 'Captain1',
      awayCaptain: 'Captain2',
      originalScheduledTime: '2024-01-15T20:00:00Z',
      newScheduledTime: '2024-01-16T21:00:00Z',
      requestedByGamertag: 'Player1',
      requestedByUserID: null,
      reason: 'Schedule conflict',
      requestedAt: '2024-01-14T10:00:00Z',
      status: 'Pending',
      discordThreadID: null,
      canApprove: true,
      whyNotApprovable: null,
    };

    component.createDiscordThread(mockReschedule);

    const req = httpTestingController.expectOne(`/api/reschedules/${mockReschedule.matchRescheduleID}/discord-thread`);
    expect(req.request.method).toBe('POST');
    req.flush({});
    
    // After the POST completes, the component calls loadDashboardData()
    const refreshReq = httpTestingController.expectOne('/api/commissioner-dashboard');
    refreshReq.flush({ pendingReschedules: [], overdueMatches: [], summary: { pendingRescheduleCount: 0, overdueMatchCount: 0, criticalOverdueCount: 0 } });
  });

  describe('reschedule actions', () => {
    const reschedule = { matchRescheduleID: 4, seasonMatchID: 40, homeCaptain: 'A', awayCaptain: 'B',
      reason: 'r', requestedByGamertag: 'p', requestedAt: '2024-01-01T00:00:00Z', status: 'Pending', canApprove: true } as RescheduleDto;
    const empty = { pendingReschedules: [], overdueMatches: [], summary: { pendingRescheduleCount: 0, overdueMatchCount: 0, criticalOverdueCount: 0 } };

    it('reloads the dashboard after the dialog reports a processed request', () => {
      const ref = jasmine.createSpyObj('MatDialogRef', ['afterClosed']);
      ref.afterClosed.and.returnValue(of(true));
      spyOn(component['dialog'], 'open').and.returnValue(ref);

      component.openProcessRescheduleDialog(reschedule);

      expect(component['dialog'].open).toHaveBeenCalledWith(jasmine.any(Function), { width: '600px', data: reschedule });
      httpTestingController.expectOne('/api/commissioner-dashboard').flush(empty);
    });

    it('does not reload when the dialog is cancelled', () => {
      const ref = jasmine.createSpyObj('MatDialogRef', ['afterClosed']);
      ref.afterClosed.and.returnValue(of(false));
      spyOn(component['dialog'], 'open').and.returnValue(ref);

      component.openProcessRescheduleDialog(reschedule);
      expect(ref.afterClosed).toHaveBeenCalled();
      httpTestingController.expectNone('/api/commissioner-dashboard');
    });

    it('still reloads and logs when creating the Discord thread fails', () => {
      const err = spyOn(console, 'error');
      component.createDiscordThread(reschedule);
      httpTestingController.expectOne('/api/reschedules/4/discord-thread').flush('x', { status: 500, statusText: 'Server Error' });

      httpTestingController.expectOne('/api/commissioner-dashboard').flush(empty);
      expect(err).toHaveBeenCalledWith('Failed to create Discord thread:', jasmine.anything());
    });
  });

  it('renders the dashboard once it arrives, without a manual detectChanges', async () => {
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading dashboard data...');

    httpTestingController.expectOne('/api/commissioner-dashboard').flush({
      pendingReschedules: [], overdueMatches: [],
      summary: { pendingRescheduleCount: 4, overdueMatchCount: 0, criticalOverdueCount: 0 },
    });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).not.toContain('Loading dashboard data...');
    expect((fixture.nativeElement as HTMLElement).querySelector('.metric-value')?.textContent?.trim()).toBe('4');
  });

  it('links each overdue match to its match page and marks how late it is', async () => {
    await fixture.whenStable();
    httpTestingController.expectOne('/api/commissioner-dashboard').flush({
      pendingReschedules: [],
      overdueMatches: [{ seasonMatchID: 33, homeCaptain: 'A', awayCaptain: 'B', scheduledTime: '2026-10-27T00:30:00Z', hoursOverdue: 80 }],
      summary: { pendingRescheduleCount: 0, overdueMatchCount: 1, criticalOverdueCount: 1 },
    });
    await fixture.whenStable();
    (Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('[role="tab"]'))[1]).click();
    await fixture.whenStable();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a.cd-match')!.getAttribute('href')).toBe('/seasonmatch/33');
    expect(el.querySelector('.cd-severity')!.getAttribute('data-severity')).toBe('critical');
    expect(el.querySelector('.cd-metric--critical .metric-value')!.textContent!.trim()).toBe('1');
  });

  it('shows the error once the request fails, without a manual detectChanges', async () => {
    spyOn(console, 'error');
    await fixture.whenStable();
    httpTestingController.expectOne('/api/commissioner-dashboard').flush('x', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Failed to load dashboard data');
  });

  it('offers only Reject, and says why, for a request whose match can no longer move', async () => {
    const base = { seasonMatchID: 40, homeCaptain: 'A', awayCaptain: 'B', reason: 'r', requestedByGamertag: 'p', requestedByUserID: null,
      requestedAt: '2024-01-01T00:00:00Z', status: 'Pending' as const, originalScheduledTime: null, newScheduledTime: null, discordThreadID: null };
    const decided: RescheduleDto = { ...base, matchRescheduleID: 7, canApprove: false, whyNotApprovable: 'This match already has a result.' };
    const open: RescheduleDto = { ...base, matchRescheduleID: 8, canApprove: true, whyNotApprovable: null };
    await fixture.whenStable();
    httpTestingController.expectOne('/api/commissioner-dashboard').flush({
      pendingReschedules: [decided, open], overdueMatches: [],
      summary: { pendingRescheduleCount: 2, overdueMatchCount: 0, criticalOverdueCount: 0 },
    });
    await fixture.whenStable();

    const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('mat-row'));
    const decidedButton = rows[0].querySelector<HTMLButtonElement>('.mat-column-process button')!;
    const why = rows[0].querySelector('#cd-why-7')!;
    expect(decidedButton.textContent).toContain('Reject');
    expect(decidedButton.textContent).not.toContain('Process');
    expect(decidedButton.getAttribute('aria-describedby')).toBe('cd-why-7');
    expect(why.textContent).toContain('This match already has a result.');
    expect(rows[1].querySelector('.mat-column-process button')!.textContent).toContain('Process');
    expect(rows[1].querySelector('.cd-why')).toBeNull();

    spyOn(component['dialog'], 'open').and.returnValue(mockDialogRef);
    decidedButton.click();
    expect(component['dialog'].open).toHaveBeenCalledWith(jasmine.any(Function), { width: '600px', data: decided });
  });

  it('stops listening to the dialog once destroyed', () => {
    const closed = new Subject<boolean>();
    const ref = jasmine.createSpyObj('MatDialogRef', ['afterClosed']);
    ref.afterClosed.and.returnValue(closed);
    spyOn(component['dialog'], 'open').and.returnValue(ref);
    component.openProcessRescheduleDialog({ matchRescheduleID: 1 } as RescheduleDto);
    expect(closed.observed).toBeTrue();

    fixture.destroy();

    expect(closed.observed).toBeFalse();
  });
});
