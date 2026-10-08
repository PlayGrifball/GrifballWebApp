import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CreateUserComponent } from './createUser.component';

describe('CreateUserComponent', () => {
  let fixture: ComponentFixture<CreateUserComponent>;
  let component: CreateUserComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [CreateUserComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CreateUserComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  async function type(name: string, value: string): Promise<void> {
    const input = el().querySelector(`input[name="${name}"]`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('requires display name, gamertag and user name', async () => {
    const submit = el().querySelector('button') as HTMLButtonElement;
    expect(submit.disabled).toBeTrue();

    await type('displayName', 'Grif');
    await type('gamertag', 'SgtGrif');
    expect(submit.disabled).toBeTrue();

    await type('userName', 'grif');
    expect(submit.disabled).toBeFalse();
    expect(component.model).toEqual({ displayName: 'Grif', gamertag: 'SgtGrif', userName: 'grif' });
  });

  it('shows "Required" once a required field is cleared', async () => {
    await type('gamertag', 'x');
    await type('gamertag', '');
    expect(el().textContent).toContain('Required');
  });

  it('posts the new user and confirms', async () => {
    await type('displayName', 'Grif');
    await type('gamertag', 'SgtGrif');
    await type('userName', 'grif');
    el().querySelector('form')!.dispatchEvent(new Event('submit'));

    const req = http.expectOne('/api/usermanagement/createuser');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ displayName: 'Grif', gamertag: 'SgtGrif', userName: 'grif' });
    req.flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Created User');
  });

  it('reports a failure to create the user', () => {
    spyOn(console, 'log');
    component.onSubmit();
    http.expectOne('/api/usermanagement/createuser').flush('dup', { status: 409, statusText: 'Conflict' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to create user');
  });
});
