import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeedOrderingDialogComponent } from './seedOrderingDialog.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideAnimations } from '@angular/platform-browser/animations';

interface TeamStanding {
  teamID: number;
  teamName: string;
  wins: number;
  losses: number;
  seed?: number;
}

describe('SeedOrderingDialogComponent', () => {
  let component: SeedOrderingDialogComponent;
  let fixture: ComponentFixture<SeedOrderingDialogComponent>;
  let httpTestingController: HttpTestingController;
  let mockDialogRef: jasmine.SpyObj<MatDialogRef<SeedOrderingDialogComponent>>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;

  const seasonID = 10;

  beforeEach(async () => {
    mockDialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open']);

    await TestBed.configureTestingModule({
      imports: [SeedOrderingDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: MAT_DIALOG_DATA, useValue: seasonID },
        { provide: MatDialogRef, useValue: mockDialogRef },
        { provide: MatSnackBar, useValue: mockSnackBar }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SeedOrderingDialogComponent);
    component = fixture.componentInstance;
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with seasonID from injected data', () => {
    expect(component['seasonID']).toBe(10);
  });

  it('should initialize loading as true', () => {
    expect(component.loading).toBe(true);
  });

  it('should load team standings on init', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4 },
      { teamID: 3, teamName: 'Team C', wins: 6, losses: 6 }
    ];

    fixture.detectChanges();

    const req = httpTestingController.expectOne(`api/TeamStandings/GetTeamStandings/${seasonID}`);
    expect(req.request.method).toBe('GET');
    req.flush(mockStandings);

    expect(component.teams.length).toBe(3);
    expect(component.teams[0].seed).toBe(1);
    expect(component.teams[1].seed).toBe(2);
    expect(component.teams[2].seed).toBe(3);
    expect(component.loading).toBe(false);
  });

  it('should handle team standings load error', () => {
    fixture.detectChanges();

    const req = httpTestingController.expectOne(`api/TeamStandings/GetTeamStandings/${seasonID}`);
    req.error(new ProgressEvent('error'));

    expect(mockSnackBar.open).toHaveBeenCalledWith('Failed to load team standings', 'Close');
    expect(component.loading).toBe(false);
  });

  it('should handle drag and drop to reorder teams', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4, seed: 2 },
      { teamID: 3, teamName: 'Team C', wins: 6, losses: 6, seed: 3 }
    ];

    component.teams = [...mockStandings];

    const dragEvent = {
      dropEffect: 'move',
      data: mockStandings[0], // Team A
      index: 2 // Drop at position 2 (becoming 3rd)
    };

    component.onDrop(dragEvent as any);

    expect(component.teams[0].teamID).toBe(2); // Team B is now first
    expect(component.teams[1].teamID).toBe(3); // Team C is now second
    expect(component.teams[2].teamID).toBe(1); // Team A is now third
    expect(component.teams[0].seed).toBe(1);
    expect(component.teams[1].seed).toBe(2);
    expect(component.teams[2].seed).toBe(3);
  });

  it('should handle drag and drop moving up in list', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4, seed: 2 },
      { teamID: 3, teamName: 'Team C', wins: 6, losses: 6, seed: 3 }
    ];

    component.teams = [...mockStandings];

    const dragEvent = {
      dropEffect: 'move',
      data: mockStandings[2], // Team C
      index: 0 // Drop at position 0 (becoming 1st)
    };

    component.onDrop(dragEvent as any);

    expect(component.teams[0].teamID).toBe(3); // Team C is now first
    expect(component.teams[0].seed).toBe(1);
  });

  it('should not modify teams if drag event has no dropEffect', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4, seed: 2 }
    ];

    component.teams = [...mockStandings];
    const originalTeams = [...component.teams];

    const dragEvent = {
      dropEffect: 'copy',
      data: mockStandings[0],
      index: 1
    };

    component.onDrop(dragEvent as any);

    expect(component.teams).toEqual(originalTeams);
  });

  it('should not modify teams if dragged team not found', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4, seed: 2 }
    ];

    component.teams = [...mockStandings];
    const originalTeams = [...component.teams];

    const dragEvent = {
      dropEffect: 'move',
      data: { teamID: 999, teamName: 'Non-existent', wins: 0, losses: 0 },
      index: 0
    };

    component.onDrop(dragEvent as any);

    expect(component.teams).toEqual(originalTeams);
  });

  it('should not modify teams if index is undefined', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4, seed: 2 }
    ];

    component.teams = [...mockStandings];
    const originalTeams = [...component.teams];

    const dragEvent = {
      dropEffect: 'move',
      data: mockStandings[0],
      index: undefined
    };

    component.onDrop(dragEvent as any);

    expect(component.teams).toEqual(originalTeams);
  });

  it('should submit custom seeds and close dialog', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4, seed: 2 }
    ];

    component.teams = mockStandings;
    component['seasonID'] = seasonID;

    const seedingOrderSpy = spyOn(component.seedingOrder, 'emit');

    component.onSubmit();

    const req = httpTestingController.expectOne(`api/Brackets/SetCustomSeeds/${seasonID}`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual([
      { teamID: 1, seed: 1 },
      { teamID: 2, seed: 2 }
    ]);
    req.flush({});

    expect(seedingOrderSpy).toHaveBeenCalled();
    expect(mockDialogRef.close).toHaveBeenCalledWith(true);
  });

  it('should handle submit custom seeds error', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 }
    ];

    component.teams = mockStandings;
    component['seasonID'] = seasonID;

    component.onSubmit();

    const req = httpTestingController.expectOne(`api/Brackets/SetCustomSeeds/${seasonID}`);
    req.error(new ProgressEvent('error'));

    expect(mockSnackBar.open).toHaveBeenCalledWith('Failed to set custom seeds', 'Close');
  });

  it('should close dialog on cancel', () => {
    component.onCancel();

    expect(mockDialogRef.close).toHaveBeenCalledWith(false);
  });

  it('should adjust index when dragging down in same list', () => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 },
      { teamID: 2, teamName: 'Team B', wins: 8, losses: 4, seed: 2 },
      { teamID: 3, teamName: 'Team C', wins: 6, losses: 6, seed: 3 },
      { teamID: 4, teamName: 'Team D', wins: 4, losses: 8, seed: 4 }
    ];

    component.teams = [...mockStandings];

    // Drag Team A (index 0) to position 2 (becomes 3rd)
    const dragEvent = {
      dropEffect: 'move',
      data: mockStandings[0],
      index: 3
    };

    component.onDrop(dragEvent as any);

    // After adjustment, Team A should be at index 2
    expect(component.teams[2].teamID).toBe(1);
    expect(component.teams[2].seed).toBe(3);
  });

  it('should emit seedingOrder event when seeds are successfully set', (done) => {
    const mockStandings: TeamStanding[] = [
      { teamID: 1, teamName: 'Team A', wins: 10, losses: 2, seed: 1 }
    ];

    component.teams = mockStandings;
    component['seasonID'] = seasonID;

    component.seedingOrder.subscribe(() => {
      expect(true).toBe(true);
      done();
    });

    component.onSubmit();

    const req = httpTestingController.expectOne(`api/Brackets/SetCustomSeeds/${seasonID}`);
    req.flush({});
  });

  it('should handle empty teams array on submit', () => {
    component.teams = [];
    component['seasonID'] = seasonID;

    component.onSubmit();

    const req = httpTestingController.expectOne(`api/Brackets/SetCustomSeeds/${seasonID}`);
    expect(req.request.body).toEqual([]);
    req.flush({});

    expect(mockDialogRef.close).toHaveBeenCalledWith(true);
  });
});
