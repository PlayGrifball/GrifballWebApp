import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CreateBracketDialogComponent } from './createBracketDialog.component';

describe('CreateBracketDialogComponent', () => {
  let fixture: ComponentFixture<CreateBracketDialogComponent>;
  let component: CreateBracketDialogComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [CreateBracketDialogComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: 3 },
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CreateBracketDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const submitButton = () => (fixture.nativeElement as HTMLElement).querySelector('button') as HTMLButtonElement;

  it('takes the season from the dialog data and defaults to an 8-team double-elimination best of 3', () => {
    expect(component.seasonID).toBe(3);
    expect(component.participantsCount).toBe(8);
    expect(component.doubleElimination).toBeTrue();
    expect(component.bestOf).toBe(3);
    expect(submitButton().disabled).toBeFalse();
  });

  it('disables the submit button when a required number is cleared', async () => {
    const input = (fixture.nativeElement as HTMLElement).querySelector('input[name="participantsCount"]') as HTMLInputElement;
    input.value = '';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.participantsCount).toBeNull();
    expect(submitButton().disabled).toBeTrue();
  });

  it('creates the bracket with the chosen options and emits bracketCreated', () => {
    const created = jasmine.createSpy('bracketCreated');
    component.bracketCreated.subscribe(created);
    component.participantsCount = 4;
    component.doubleElimination = false;
    component.bestOf = 5;

    component.onSubmit();
    const req = http.expectOne('api/Brackets/CreateBracket?seasonID=3&participantsCount=4&doubleElimination=false&bestOf=5');
    expect(req.request.method).toBe('GET');
    req.flush({});

    expect(created).toHaveBeenCalled();
    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it('reports a failure and does not emit', () => {
    spyOn(console, 'log');
    const created = jasmine.createSpy('bracketCreated');
    component.bracketCreated.subscribe(created);

    component.onSubmit();
    http.expectOne(r => r.url.startsWith('api/Brackets/CreateBracket')).flush('x', { status: 400, statusText: 'Bad Request' });

    expect(created).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to create bracket', 'Close');
  });
});
