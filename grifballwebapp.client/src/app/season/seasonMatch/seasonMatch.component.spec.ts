import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeasonMatchComponent } from './seasonMatch.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AccountService } from '../../account.service';
import { SeasonMatchPageDto } from './seasonMatchPageDto';
import { PossibleMatchDto, PossiblePlayerDto } from './possibleMatchDto';
import { ComponentRef } from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('SeasonMatchComponent', () => {
  let component: SeasonMatchComponent;
  let fixture: ComponentFixture<SeasonMatchComponent>;
  let httpTestingController: HttpTestingController;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let componentRef: ComponentRef<SeasonMatchComponent>;

  beforeEach(async () => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['isLoggedIn']);

    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: AccountService, useValue: mockAccountService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SeasonMatchComponent);
    component = fixture.componentInstance;
    componentRef = fixture.componentRef;
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with required seasonMatchID input', () => {
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    expect(component.seasonMatchID()).toBe(123);
  });

  it('should fetch page DTO on init', () => {
    const mockPageDto: SeasonMatchPageDto = {
      seasonID: 1,
      seasonName: 'Season 1',
      isPlayoff: false,
      homeTeamName: 'Home Team',
      homeTeamID: 1,
      homeTeamScore: 0,
      homeTeamResult: 'TBD',
      awayTeamName: 'Away Team',
      awayTeamID: 2,
      awayTeamScore: 0,
      awayTeamResult: 'TBD',
      scheduledTime: null,
      bestOf: 3,
      reportedGames: [],
      bracketInfo: null,
      activeRescheduleRequestId: null
    };

    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();

    const req = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    expect(req.request.method).toBe('GET');
    req.flush(mockPageDto);

    expect(component.seasonMatch()).toEqual(mockPageDto);
  });

  it('should fetch possible matches on init', () => {
    const mockPossibleMatches: PossibleMatchDto[] = [
      {
        matchID: 'match-guid-1',
        homeTeam: {
          teamID: 1,
          score: 3,
          outcome: 1,
          players: []
        },
        awayTeam: {
          teamID: 2,
          score: 2,
          outcome: 0,
          players: []
        }
      }
    ];

    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();

    const req1 = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    req1.flush({});

    const req2 = httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123');
    expect(req2.request.method).toBe('GET');
    req2.flush(mockPossibleMatches);

    expect(component.possibleMatches()).toEqual(mockPossibleMatches);
  });

  it('should have correct displayedColumns', () => {
    expect(component.displayedColumns).toEqual(['gamertag', 'score', 'kills', 'deaths']);
  });

  it('should have correct column definitions', () => {
    expect(component.columns.length).toBe(4);
    expect(component.columns[0].columnDef).toBe('gamertag');
    expect(component.columns[1].columnDef).toBe('score');
    expect(component.columns[2].columnDef).toBe('kills');
    expect(component.columns[3].columnDef).toBe('deaths');
  });

  it('should format column cells correctly', () => {
    const mockPlayer: PossiblePlayerDto = {
      xboxUserID: 1,
      gamertag: 'TestPlayer',
      score: 100,
      kills: 10,
      deaths: 5,
      isOnTeam: true
    };

    expect(component.columns[0].cell(mockPlayer)).toBe('TestPlayer');
    expect(component.columns[1].cell(mockPlayer)).toBe('100');
    expect(component.columns[2].cell(mockPlayer)).toBe('10');
    expect(component.columns[3].cell(mockPlayer)).toBe('5');
  });

  it('should have correct regex pattern for GUID validation', () => {
    expect(component.regex).toBe(String.raw`^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$`);
    expect(component.regexErrorMessage).toBe('Must be a valid GUID');
  });

  it('should submit match and refresh data', () => {
    const matchGuid = '12345678-1234-1234-1234-123456789012';
    
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    // Clear initial requests
    httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123').flush({});
    httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123').flush([]);

    component.onSubmit(matchGuid);

    expect(component.isSubmittingMatch()).toBe(true);

    const req = httpTestingController.expectOne('/api/SeasonMatch/ReportMatch/123/' + matchGuid);
    expect(req.request.method).toBe('GET');
    req.flush('Success');

    expect(component.isSubmittingMatch()).toBe(false);

    // Should refresh page data
    const refreshReq = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    refreshReq.flush({});
  });

  it('should handle submit match error', () => {
    const matchGuid = '12345678-1234-1234-1234-123456789012';
    
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    // Clear initial requests
    httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123').flush({});
    httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123').flush([]);

    component.onSubmit(matchGuid);

    const req = httpTestingController.expectOne('/api/SeasonMatch/ReportMatch/123/' + matchGuid);
    req.error(new ProgressEvent('error'));

    expect(component.isSubmittingMatch()).toBe(false);

    // Should still refresh page data
    const refreshReq = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    refreshReq.flush({});
  });

  it('should handle home forfeit', () => {
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    // Clear initial requests
    httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123').flush({});
    httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123').flush([]);

    component.homeForfeit();

    expect(component.isSubmittingMatch()).toBe(true);

    const req = httpTestingController.expectOne('/api/SeasonMatch/HomeForfeit/123');
    expect(req.request.method).toBe('GET');
    req.flush('Success');

    expect(component.isSubmittingMatch()).toBe(false);

    const refreshReq = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    refreshReq.flush({});
  });

  it('should handle home forfeit error', () => {
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    // Clear initial requests
    httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123').flush({});
    httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123').flush([]);

    component.homeForfeit();

    const req = httpTestingController.expectOne('/api/SeasonMatch/HomeForfeit/123');
    req.error(new ProgressEvent('error'));

    expect(component.isSubmittingMatch()).toBe(false);

    const refreshReq = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    refreshReq.flush({});
  });

  it('should handle away forfeit', () => {
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    // Clear initial requests
    httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123').flush({});
    httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123').flush([]);

    component.awayForfeit();

    expect(component.isSubmittingMatch()).toBe(true);

    const req = httpTestingController.expectOne('/api/SeasonMatch/AwayForfeit/123');
    expect(req.request.method).toBe('GET');
    req.flush('Success');

    expect(component.isSubmittingMatch()).toBe(false);

    const refreshReq = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    refreshReq.flush({});
  });

  it('should handle away forfeit error', () => {
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    // Clear initial requests
    httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123').flush({});
    httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123').flush([]);

    component.awayForfeit();

    const req = httpTestingController.expectOne('/api/SeasonMatch/AwayForfeit/123');
    req.error(new ProgressEvent('error'));

    expect(component.isSubmittingMatch()).toBe(false);

    const refreshReq = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123');
    refreshReq.flush({});
  });

  it('should initialize matchID as empty string', () => {
    expect(component.matchID).toBe('');
  });

  it('should initialize seasonMatch as null signal', () => {
    expect(component.seasonMatch()).toBe(null);
  });

  it('should initialize possibleMatches as empty array signal', () => {
    expect(component.possibleMatches()).toEqual([]);
  });

  it('should initialize isSubmittingMatch as false signal', () => {
    expect(component.isSubmittingMatch()).toBe(false);
  });

  it('should call finishedReportingMatch after submit completes', () => {
    spyOn<any>(component, 'finishedReportingMatch');
    const matchGuid = '12345678-1234-1234-1234-123456789012';
    
    componentRef.setInput('seasonMatchID', 123);
    fixture.detectChanges();
    
    // Clear initial requests
    httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/123').flush({});
    httpTestingController.expectOne('/api/SeasonMatch/GetPossibleMatches/123').flush([]);

    component.onSubmit(matchGuid);

    const req = httpTestingController.expectOne('/api/SeasonMatch/ReportMatch/123/' + matchGuid);
    req.flush('Success');

    expect(component['finishedReportingMatch']).toHaveBeenCalled();
  });

  it('should update seasonMatch signal when page DTO is fetched', () => {
    const mockPageDto: SeasonMatchPageDto = {
      seasonID: 2,
      seasonName: 'Season 2',
      isPlayoff: true,
      homeTeamName: 'Captain A Team',
      homeTeamID: 5,
      homeTeamScore: 3,
      homeTeamResult: 'Won',
      awayTeamName: 'Captain B Team',
      awayTeamID: 6,
      awayTeamScore: 1,
      awayTeamResult: 'Loss',
      scheduledTime: null,
      bestOf: 5,
      reportedGames: [],
      bracketInfo: null,
      activeRescheduleRequestId: null
    };

    componentRef.setInput('seasonMatchID', 456);
    fixture.detectChanges();

    const req = httpTestingController.expectOne('/api/SeasonMatch/GetSeasonMatchPage/456');
    req.flush(mockPageDto);

    expect(component.seasonMatch()?.homeTeamName).toBe('Captain A Team');
    expect(component.seasonMatch()?.awayTeamName).toBe('Captain B Team');
    expect(component.seasonMatch()?.homeTeamResult).toBe('Won');
    expect(component.seasonMatch()?.homeTeamScore).toBe(3);
    expect(component.seasonMatch()?.awayTeamScore).toBe(1);
  });
});
