import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { CommissionerDashboardComponent, CommissionerDashboardDto, RescheduleDto, OverdueMatchDto } from './commissioner-dashboard.component';

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
        HttpClientTestingModule,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialog, useValue: mockDialog }
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
    expect(component.loading).toBeTruthy();
    expect(component.dashboardData).toBeNull();
    expect(component.error).toBeNull();
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
          reason: 'Schedule conflict',
          requestedAt: '2024-01-14T10:00:00Z',
          status: 0
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

    const req = httpTestingController.expectOne('/api/CommissionerDashboard/GetDashboardData');
    expect(req.request.method).toBe('GET');
    req.flush(mockDashboardData);

    expect(component.loading).toBeFalsy();
    expect(component.dashboardData).toEqual(mockDashboardData);
    expect(component.error).toBeNull();
  });

  it('should handle error when loading dashboard data', () => {
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/CommissionerDashboard/GetDashboardData');
    req.flush('Error loading data', { status: 500, statusText: 'Internal Server Error' });

    expect(component.loading).toBeFalsy();
    expect(component.dashboardData).toBeNull();
    expect(component.error).toBe('Failed to load dashboard data');
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
    const result = component.formatDateTime(undefined);
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
      reason: 'Schedule conflict',
      requestedAt: '2024-01-14T10:00:00Z',
      status: 0
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
      reason: 'Schedule conflict',
      requestedAt: '2024-01-14T10:00:00Z',
      status: 0
    };

    component.createDiscordThread(mockReschedule);

    const req = httpTestingController.expectOne(`/api/Reschedule/CreateDiscordThread/${mockReschedule.matchRescheduleID}`);
    expect(req.request.method).toBe('POST');
    req.flush({});
    
    // After the POST completes, the component calls loadDashboardData()
    const refreshReq = httpTestingController.expectOne('/api/CommissionerDashboard/GetDashboardData');
    refreshReq.flush({ pendingReschedules: [], overdueMatches: [], summary: { pendingRescheduleCount: 0, overdueMatchCount: 0, criticalOverdueCount: 0 } });
  });
});