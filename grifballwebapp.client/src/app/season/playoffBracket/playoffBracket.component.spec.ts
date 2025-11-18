import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlayoffBracketComponent } from './playoffBracket.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../../account.service';
import { ViewerData } from 'brackets-viewer';
import { of } from 'rxjs';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('PlayoffBracketComponent', () => {
  let component: PlayoffBracketComponent;
  let fixture: ComponentFixture<PlayoffBracketComponent>;
  let httpTestingController: HttpTestingController;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockDialog: jasmine.SpyObj<MatDialog>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;
  let mockAccountService: jasmine.SpyObj<AccountService>;

  const mockActivatedRoute = {
    snapshot: {
      paramMap: {
        get: jasmine.createSpy('get').and.returnValue('5')
      }
    }
  };

  beforeEach(async () => {
    mockRouter = jasmine.createSpy('Router') as any;
    mockRouter.navigate = jasmine.createSpy('navigate');
    mockDialog = jasmine.createSpyObj('MatDialog', ['open']);
    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    mockAccountService = jasmine.createSpyObj('AccountService', ['isLoggedIn']);

    // Setup window.bracketsViewer mock
    (window as any).bracketsViewer = {
      render: jasmine.createSpy('render'),
      addLocale: jasmine.createSpy('addLocale'),
      onMatchClicked: null
    };

    await TestBed.configureTestingModule({
      imports: [PlayoffBracketComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        { provide: Router, useValue: mockRouter },
        { provide: MatDialog, useValue: mockDialog },
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: AccountService, useValue: mockAccountService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PlayoffBracketComponent);
    component = fixture.componentInstance;
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    delete (window as any).bracketsViewer;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with seasonID from route', () => {
    fixture.detectChanges();
    
    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    req.flush({ participants: [], matches: [], stages: [], matchGames: [] });
    
    expect(component['seasonID']).toBe(5);
  });

  it('should not fetch data when seasonID is 0', () => {
    mockActivatedRoute.snapshot.paramMap.get = jasmine.createSpy('get').and.returnValue('0');
    const http = TestBed.inject(HttpClient);
    const component2 = new PlayoffBracketComponent(
      mockActivatedRoute as any, 
      http,
      mockRouter,
      mockDialog,
      mockSnackBar,
      mockAccountService
    );
    component2.ngOnInit();
    
    httpTestingController.expectNone('api/Brackets/GetViewerData?seasonID=0');
  });

  it('should fetch viewer data on init', () => {
    const mockViewerData: ViewerData = {
      participants: [{ id: 1, tournament_id: 1, name: 'Team 1' }],
      matches: [],
      stages: [],
      matchGames: []
    };

    fixture.detectChanges();

    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    expect(req.request.method).toBe('GET');
    req.flush(mockViewerData);

    expect(component.hasBracket()).toBe(true);
  });

  it('should set hasBracket to false when no participants', () => {
    const mockViewerData: ViewerData = {
      participants: [],
      matches: [],
      stages: [],
      matchGames: []
    };

    fixture.detectChanges();

    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    req.flush(mockViewerData);

    expect(component.hasBracket()).toBe(false);
  });

  it('should render bracket with viewer data', () => {
    const mockViewerData: ViewerData = {
      participants: [{ id: 1, tournament_id: 1, name: 'Team 1' }],
      matches: [],
      stages: [{ id: 1, tournament_id: 1, name: 'Main Stage', type: 'single_elimination', number: 1, settings: {} }],
      matchGames: []
    };

    fixture.detectChanges();

    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    req.flush(mockViewerData);

    expect((window as any).bracketsViewer.render).toHaveBeenCalledWith(mockViewerData, { clear: true });
    expect((window as any).bracketsViewer.addLocale).toHaveBeenCalled();
  });

  it('should handle viewer data fetch error', () => {
    fixture.detectChanges();

    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    req.error(new ProgressEvent('error'));

    expect(mockSnackBar.open).toHaveBeenCalledWith('Failed to get viewer data', 'Close');
  });

  it('should open create bracket dialog', () => {
    const mockDialogRef = {
      componentInstance: {
        bracketCreated: of(void 0)
      }
    };
    mockDialog.open.and.returnValue(mockDialogRef as any);

    component['seasonID'] = 5;
    component.openDialog();

    expect(mockDialog.open).toHaveBeenCalled();
  });

  it('should refresh viewer data when bracket is created', (done) => {
    const mockDialogRef = {
      componentInstance: {
        bracketCreated: of(void 0)
      }
    };
    mockDialog.open.and.returnValue(mockDialogRef as any);

    component['seasonID'] = 5;
    component.openDialog();

    setTimeout(() => {
      const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
      req.flush({ participants: [], matches: [], stages: [], matchGames: [] });
      done();
    }, 100);
  });

  it('should open seed ordering dialog', () => {
    const mockDialogRef = {
      componentInstance: {
        seedingOrder: of(void 0)
      }
    };
    mockDialog.open.and.returnValue(mockDialogRef as any);

    component['seasonID'] = 5;
    component.setSeeds();

    expect(mockDialog.open).toHaveBeenCalledWith(jasmine.anything(), {
      data: 5,
      width: '600px',
      maxHeight: '80vh'
    });
  });

  it('should refresh viewer data when seeds are set', (done) => {
    const mockDialogRef = {
      componentInstance: {
        seedingOrder: of(void 0)
      }
    };
    mockDialog.open.and.returnValue(mockDialogRef as any);

    component['seasonID'] = 5;
    component.setSeeds();

    setTimeout(() => {
      const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
      req.flush({ participants: [], matches: [], stages: [], matchGames: [] });
      done();
    }, 100);
  });

  it('should navigate to match when bracket match is clicked', () => {
    const mockViewerData: ViewerData = {
      participants: [{ id: 1, tournament_id: 1, name: 'Team 1' }],
      matches: [],
      stages: [],
      matchGames: []
    };

    fixture.detectChanges();

    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    req.flush(mockViewerData);

    // Simulate match click
    const mockMatch = { id: 123 } as any;
    (window as any).bracketsViewer.onMatchClicked(mockMatch);

    expect(mockRouter.navigate).toHaveBeenCalledWith(['/seasonmatch/123']);
  });

  it('should add custom locale for bracket viewer', () => {
    const mockViewerData: ViewerData = {
      participants: [{ id: 1, tournament_id: 1, name: 'Team 1' }],
      matches: [],
      stages: [],
      matchGames: []
    };

    fixture.detectChanges();

    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    req.flush(mockViewerData);

    expect((window as any).bracketsViewer.addLocale).toHaveBeenCalledWith('en', jasmine.objectContaining({
      common: jasmine.any(Object),
      'origin-hint': jasmine.any(Object)
    }));
  });

  it('should initialize hasBracket signal as false', () => {
    expect(component.hasBracket()).toBe(false);
  });

  it('should call getViewerData on ngOnInit', () => {
    spyOn(component, 'getViewerData');
    
    component.ngOnInit();
    
    expect(component.getViewerData).toHaveBeenCalled();
  });

  it('should handle multiple participants in viewer data', () => {
    const mockViewerData: ViewerData = {
      participants: [
        { id: 1, tournament_id: 1, name: 'Team 1' },
        { id: 2, tournament_id: 1, name: 'Team 2' },
        { id: 3, tournament_id: 1, name: 'Team 3' }
      ],
      matches: [],
      stages: [],
      matchGames: []
    };

    fixture.detectChanges();

    const req = httpTestingController.expectOne('api/Brackets/GetViewerData?seasonID=5');
    req.flush(mockViewerData);

    expect(component.hasBracket()).toBe(true);
    expect((window as any).bracketsViewer.render).toHaveBeenCalled();
  });
});
