import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { EditUserComponent } from './editUser.component';
import { UserResponseDto } from '../userResponseDto';

describe('EditUserComponent', () => {
  let fixture: ComponentFixture<EditUserComponent>;
  let component: EditUserComponent;
  let http: HttpTestingController;
  let userID: string;

  const user: UserResponseDto = {
    userID: 15,
    userName: 'grif',
    lockoutEnd: null,
    lockoutEnabled: false,
    isDummyUser: false,
    accessFailedCount: 0,
    region: null,
    displayName: 'Grif',
    gamertag: 'SgtGrif',
    discord: null,
    externalAuthCount: 1,
    hasPassword: true,
    roles: [{ roleName: 'Sysadmin', hasRole: false }, { roleName: 'Commissioner', hasRole: true }],
  };

  beforeEach(async () => {
    userID = '15';
    await TestBed.configureTestingModule({
      imports: [EditUserComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideLuxonDatetimeAdapter(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => userID } } } },
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(EditUserComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => http.verify());

  async function load(): Promise<void> {
    fixture.detectChanges();
    http.expectOne('/api/usermanagement/getuser/15').flush(structuredClone(user));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;

  it('does not fetch anything for user id 0', () => {
    userID = '0';
    const log = spyOn(console, 'log');
    fixture.detectChanges();
    http.expectNone(() => true);
    expect(log).toHaveBeenCalledWith('Invalid user ID');
  });

  it('loads the user and renders a checkbox per role', async () => {
    await load();

    expect(el().querySelector('h2')!.textContent).toBe('Edit User 15');
    expect(component.model.userName).toBe('grif');
    const labels = Array.from(el().querySelectorAll('mat-checkbox')).map(c => c.textContent!.trim());
    expect(labels).toEqual(['Lockout Enabled', 'Is Dummy User', 'Sysadmin', 'Commissioner']);
  });

  it('logs a failed load', () => {
    const log = spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('/api/usermanagement/getuser/15').flush('x', { status: 404, statusText: 'Not Found' });
    expect(log).toHaveBeenCalled();
  });

  it('posts the edited user, including role changes', async () => {
    await load();
    spyOn(console, 'log');
    component.model.roles[0].hasRole = true;
    component.model.displayName = 'Private Grif';

    component.onSubmit();
    const req = http.expectOne('/api/usermanagement/edituser/');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.displayName).toBe('Private Grif');
    expect(req.request.body.roles).toEqual([{ roleName: 'Sysadmin', hasRole: true }, { roleName: 'Commissioner', hasRole: true }]);
    req.flush({});
    expect(console.log).toHaveBeenCalledWith('Updated user');
  });

  it('logs a failed update', async () => {
    await load();
    const log = spyOn(console, 'log');
    component.onSubmit();
    http.expectOne('/api/usermanagement/edituser/').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(log).toHaveBeenCalled();
    expect(log).not.toHaveBeenCalledWith('Updated user');
  });
});
