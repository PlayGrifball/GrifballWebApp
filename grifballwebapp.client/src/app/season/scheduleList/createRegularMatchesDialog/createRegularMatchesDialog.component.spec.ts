import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CreateRegularMatchesDialogComponent } from './createRegularMatchesDialog.component';

describe('CreateRegularMatchesDialogComponent', () => {
  let fixture: ComponentFixture<CreateRegularMatchesDialogComponent>;
  let component: CreateRegularMatchesDialogComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [CreateRegularMatchesDialogComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: 11 },
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CreateRegularMatchesDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('defaults to one home match per team, best of one, for the dialog season', () => {
    expect(component.seasonID).toBe(11);
    expect(component.homeMatchesPerTeam).toBe(1);
    expect(component.bestOf).toBe(1);
  });

  it('disables the button while the form is invalid', async () => {
    const el = fixture.nativeElement as HTMLElement;
    const button = el.querySelector('button') as HTMLButtonElement;
    expect(button.disabled).toBeFalse();

    const input = el.querySelector('input[name="bestOf"]') as HTMLInputElement;
    input.value = '';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(button.disabled).toBeTrue();
  });

  it('submits the form values and emits regularMatchesCreated', () => {
    const created = jasmine.createSpy('regularMatchesCreated');
    component.regularMatchesCreated.subscribe(created);
    component.homeMatchesPerTeam = 2;
    component.bestOf = 3;

    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));
    http.expectOne('api/MatchPlanner/CreateSeasonMatches?seasonID=11&homeMatchesPerTeam=2&bestOf=3').flush({});

    expect(created).toHaveBeenCalled();
  });

  it('reports a failure and does not emit', () => {
    spyOn(console, 'log');
    const created = jasmine.createSpy('regularMatchesCreated');
    component.regularMatchesCreated.subscribe(created);

    component.onSubmit();
    http.expectOne(r => r.url.startsWith('api/MatchPlanner/CreateSeasonMatches')).flush('x', { status: 500, statusText: 'Server Error' });

    expect(created).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to create regular matches', 'Close');
  });
});
