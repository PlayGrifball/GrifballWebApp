import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeedOrderingDialogComponent } from './seedOrderingDialog.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('SeedOrderingDialogComponent', () => {
  let component: SeedOrderingDialogComponent;
  let fixture: ComponentFixture<SeedOrderingDialogComponent>;

  beforeEach(async () => {
    const mockDialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [SeedOrderingDialogComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: MatDialogRef, useValue: mockDialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { seasonID: 'test-season-id' } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SeedOrderingDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

describe('SeedOrderingDialogComponent behaviour', () => {
  let fixture: ComponentFixture<SeedOrderingDialogComponent>;
  let component: SeedOrderingDialogComponent;
  let http: HttpTestingController;
  let dialogRef: jasmine.SpyObj<MatDialogRef<SeedOrderingDialogComponent>>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  const standings = [
    { teamID: 1, teamName: 'Alpha', wins: 5, losses: 0 },
    { teamID: 2, teamName: 'Bravo', wins: 3, losses: 2 },
    { teamID: 3, teamName: 'Charlie', wins: 1, losses: 4 },
  ];

  beforeEach(async () => {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [SeedOrderingDialogComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: 9 },
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeedOrderingDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function loadStandings(): void {
    http.expectOne('api/TeamStandings/GetTeamStandings/9').flush(standings.map(s => ({ ...s })));
    fixture.detectChanges();
  }

  const names = () => component.teams.map(t => `${t.seed}:${t.teamName}`);

  it('shows a loading message and disables submit until standings arrive', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Loading team standings...');
    const submit = Array.from(el.querySelectorAll('button')).find(b => b.textContent!.includes('Set Seeds'))!;
    expect(submit.disabled).toBeTrue();

    loadStandings();
    expect(submit.disabled).toBeFalse();
  });

  it('seeds teams in standings order and renders their records', () => {
    loadStandings();

    expect(component.loading).toBeFalse();
    expect(names()).toEqual(['1:Alpha', '2:Bravo', '3:Charlie']);
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Alpha');
    expect(text).toContain('5W - 0L');
    expect(text).toContain('1W - 4L');
  });

  it('reports a failure to load standings', () => {
    spyOn(console, 'log');
    http.expectOne('api/TeamStandings/GetTeamStandings/9').flush('x', { status: 500, statusText: 'Server Error' });

    expect(component.loading).toBeFalse();
    expect(component.teams).toEqual([]);
    expect(snackBar.open).toHaveBeenCalledWith('Failed to load team standings', 'Close');
  });

  it('moves a team up and renumbers the seeds', () => {
    loadStandings();
    component.onDrop({ dropEffect: 'move', data: component.teams[2], index: 0 } as any);

    expect(names()).toEqual(['1:Charlie', '2:Alpha', '3:Bravo']);
  });

  it('moves a team down, accounting for its removal from above the drop point', () => {
    loadStandings();
    component.onDrop({ dropEffect: 'move', data: component.teams[0], index: 2 } as any);

    expect(names()).toEqual(['1:Bravo', '2:Alpha', '3:Charlie']);
  });

  it('ignores drops that are not moves, have no index, or carry an unknown team', () => {
    loadStandings();
    component.onDrop({ dropEffect: 'copy', data: component.teams[2], index: 0 } as any);
    component.onDrop({ dropEffect: 'move', data: component.teams[2], index: undefined } as any);
    component.onDrop({ dropEffect: 'move', data: { teamID: 99 }, index: 0 } as any);

    expect(names()).toEqual(['1:Alpha', '2:Bravo', '3:Charlie']);
  });

  it('submits the custom seeds, emits and closes the dialog with true', () => {
    loadStandings();
    component.onDrop({ dropEffect: 'move', data: component.teams[2], index: 0 } as any);
    const emitted = jasmine.createSpy('seedingOrder');
    component.seedingOrder.subscribe(emitted);

    component.onSubmit();
    const req = http.expectOne('api/Brackets/SetCustomSeeds/9');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual([
      { teamID: 3, seed: 1 },
      { teamID: 1, seed: 2 },
      { teamID: 2, seed: 3 },
    ]);
    req.flush({});

    expect(emitted).toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('keeps the dialog open and reports when saving the seeds fails', () => {
    loadStandings();
    spyOn(console, 'log');
    component.onSubmit();
    http.expectOne('api/Brackets/SetCustomSeeds/9').flush('x', { status: 400, statusText: 'Bad Request' });

    expect(snackBar.open).toHaveBeenCalledWith('Failed to set custom seeds', 'Close');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('closes with false on cancel', () => {
    loadStandings();
    component.onCancel();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });
});
