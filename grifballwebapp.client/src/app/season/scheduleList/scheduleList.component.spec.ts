import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ScheduleListComponent } from './scheduleList.component';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { AccountService } from '../../account.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { UnscheduledMatchDto } from './unscheduledMatchDto';
import { ScheduledMatchDto } from './scheduledMatchDto';
import { provideAnimations } from '@angular/platform-browser/animations';
import { DateTime } from 'luxon';

describe('ScheduleListComponent', () => {
  let component: ScheduleListComponent;
  let fixture: ComponentFixture<ScheduleListComponent>;
  let httpTestingController: HttpTestingController;
  let mockDialog: jasmine.SpyObj<MatDialog>;
  let mockAccountService: jasmine.SpyObj<AccountService>;

  const mockActivatedRoute = {
    snapshot: {
      paramMap: {
        get: jasmine.createSpy('get').and.returnValue('1')
      }
    }
  };

  beforeEach(async () => {
    mockDialog = jasmine.createSpyObj('MatDialog', ['open']);
    mockAccountService = jasmine.createSpyObj('AccountService', ['isLoggedIn']);

    await TestBed.configureTestingModule({
      imports: [ScheduleListComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        { provide: MatDialog, useValue: mockDialog },
        { provide: AccountService, useValue: mockAccountService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ScheduleListComponent);
    component = fixture.componentInstance;
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with seasonID from route', () => {
    fixture.detectChanges();
    expect(component['seasonID']).toBe(1);
  });

  it('should not fetch matches when seasonID is 0', () => {
    mockActivatedRoute.snapshot.paramMap.get = jasmine.createSpy('get').and.returnValue('0');
    const component2 = new ScheduleListComponent(mockActivatedRoute as any, TestBed.inject(HttpClient), mockAccountService, mockDialog);
    component2.ngOnInit();
    
    // No HTTP requests should be made
    httpTestingController.expectNone('/api/MatchPlanner/GetUnscheduledMatches/0');
  });

  it('should fetch unscheduled matches on init', () => {
    const mockMatches: UnscheduledMatchDto[] = [
      { seasonMatchID: 1, homeCaptain: 'Home1', awayCaptain: 'Away1', complete: false, time: DateTime.now() }
    ];

    fixture.detectChanges();

    const req1 = httpTestingController.expectOne('/api/MatchPlanner/GetUnscheduledMatches/1');
    expect(req1.request.method).toBe('GET');
    req1.flush(mockMatches);

    const req2 = httpTestingController.expectOne('/api/MatchPlanner/GetScheduledMatches/1');
    req2.flush([]);

    expect(component.unscheduledMatches).toEqual(mockMatches);
  });

  it('should fetch scheduled matches on init', () => {
    const mockMatches: any[] = [
      { 
        seasonMatchID: 1, 
        homeCaptain: 'Home1', 
        awayCaptain: 'Away1', 
        complete: false,
        time: '2024-01-15T18:00:00Z'
      }
    ];

    fixture.detectChanges();

    const req1 = httpTestingController.expectOne('/api/MatchPlanner/GetUnscheduledMatches/1');
    req1.flush([]);

    const req2 = httpTestingController.expectOne('/api/MatchPlanner/GetScheduledMatches/1');
    req2.flush(mockMatches);

    expect(component.scheduledMatches.length).toBeGreaterThan(0);
  });

  it('should group scheduled matches by week', () => {
    const mockMatches: any[] = [
      { 
        seasonMatchID: 1, 
        homeCaptain: 'Home1', 
        awayCaptain: 'Away1', 
        complete: false,
        time: '2024-01-15T18:00:00Z'
      },
      { 
        seasonMatchID: 2, 
        homeCaptain: 'Home2', 
        awayCaptain: 'Away2', 
        complete: false,
        time: '2024-01-22T18:00:00Z'
      }
    ];

    fixture.detectChanges();

    const req1 = httpTestingController.expectOne('/api/MatchPlanner/GetUnscheduledMatches/1');
    req1.flush([]);

    const req2 = httpTestingController.expectOne('/api/MatchPlanner/GetScheduledMatches/1');
    req2.flush(mockMatches);

    expect(component.scheduledMatches.length).toBeGreaterThan(0);
    expect(component.scheduledMatches[0].weekNumber).toBe(1);
  });

  it('should toggle edit mode', () => {
    expect(component.isEditing).toBe(false);
    component.toggleEdit();
    expect(component.isEditing).toBe(true);
    component.toggleEdit();
    expect(component.isEditing).toBe(false);
  });

  it('should update match time and refresh matches', () => {
    const mockMatch: UnscheduledMatchDto = {
      seasonMatchID: 1,
      homeCaptain: 'Home1',
      awayCaptain: 'Away1',
      complete: false,
      time: DateTime.now()
    };

    component['seasonID'] = 1;
    component.onSubmit(mockMatch);

    const req = httpTestingController.expectOne('/api/MatchPlanner/UpdateMatchTime/');
    expect(req.request.method).toBe('POST');
    req.flush({});

    // Expect refresh calls
    const req1 = httpTestingController.expectOne('/api/MatchPlanner/GetUnscheduledMatches/1');
    req1.flush([]);
    const req2 = httpTestingController.expectOne('/api/MatchPlanner/GetScheduledMatches/1');
    req2.flush([]);
  });

  it('should handle update match time error and still refresh', () => {
    const mockMatch: UnscheduledMatchDto = {
      seasonMatchID: 1,
      homeCaptain: 'Home1',
      awayCaptain: 'Away1',
      complete: false,
      time: DateTime.now()
    };

    component['seasonID'] = 1;
    component.onSubmit(mockMatch);

    const req = httpTestingController.expectOne('/api/MatchPlanner/UpdateMatchTime/');
    req.error(new ProgressEvent('error'));

    // Expect refresh calls even on error
    const req1 = httpTestingController.expectOne('/api/MatchPlanner/GetUnscheduledMatches/1');
    req1.flush([]);
    const req2 = httpTestingController.expectOne('/api/MatchPlanner/GetScheduledMatches/1');
    req2.flush([]);
  });

  it('should open create regular matches dialog', () => {
    const mockDialogRef = {
      componentInstance: {
        regularMatchesCreated: of(void 0)
      }
    };
    mockDialog.open.and.returnValue(mockDialogRef as any);

    component['seasonID'] = 1;
    component.createRegularSeasonMatches();

    expect(mockDialog.open).toHaveBeenCalled();
  });

  it('should refresh matches when dialog emits regularMatchesCreated', (done) => {
    const mockDialogRef = {
      componentInstance: {
        regularMatchesCreated: of(void 0)
      }
    };
    mockDialog.open.and.returnValue(mockDialogRef as any);

    component['seasonID'] = 1;
    component.createRegularSeasonMatches();

    setTimeout(() => {
      const req1 = httpTestingController.expectOne('/api/MatchPlanner/GetUnscheduledMatches/1');
      req1.flush([]);
      const req2 = httpTestingController.expectOne('/api/MatchPlanner/GetScheduledMatches/1');
      req2.flush([]);
      done();
    }, 100);
  });

  it('should call GetMatches which calls both GetUnscheduledMatches and GetScheduledMatches', () => {
    spyOn<any>(component, 'GetUnscheduledMatches');
    spyOn<any>(component, 'GetScheduledMatches');
    
    component['GetMatches']();

    expect(component['GetUnscheduledMatches']).toHaveBeenCalled();
    expect(component['GetScheduledMatches']).toHaveBeenCalled();
  });

  it('should handle multiple matches in the same week', () => {
    const mockMatches: any[] = [
      { 
        seasonMatchID: 1, 
        homeCaptain: 'Home1', 
        awayCaptain: 'Away1', 
        complete: false,
        time: '2024-01-15T18:00:00Z'
      },
      { 
        seasonMatchID: 2, 
        homeCaptain: 'Home2', 
        awayCaptain: 'Away2', 
        complete: false,
        time: '2024-01-16T19:00:00Z'
      }
    ];

    fixture.detectChanges();

    const req1 = httpTestingController.expectOne('/api/MatchPlanner/GetUnscheduledMatches/1');
    req1.flush([]);

    const req2 = httpTestingController.expectOne('/api/MatchPlanner/GetScheduledMatches/1');
    req2.flush(mockMatches);

    expect(component.scheduledMatches.length).toBe(1);
    expect(component.scheduledMatches[0].games.length).toBe(2);
  });

  it('should properly map ScheduledMatchDto to TimeDto', () => {
    const scheduledMatch: any = {
      seasonMatchID: 1,
      homeCaptain: 'Home1',
      awayCaptain: 'Away1',
      complete: true,
      time: '2024-01-15T18:00:00Z'
    };

    const result = component['map'](scheduledMatch, 0, [scheduledMatch]);

    expect(result.SeasonMatchID).toBe(1);
    expect(result.HomeCaptainName).toBe('Home1');
    expect(result.AwayCaptainName).toBe('Away1');
    expect(result.Complete).toBe(true);
    expect(result.DateTime).toBeDefined();
    expect(result.LocalWeekNumber).toBeDefined();
    expect(result.LocalWeekYear).toBeDefined();
  });
});
