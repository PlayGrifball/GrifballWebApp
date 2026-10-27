import { DateTime } from 'luxon';
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
    expect(component.model().userName).toBe('grif');
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
    const before = component.model();
    // Edit through the form: tick the Sysadmin role and change the display name.
    Array.from(el().querySelectorAll('mat-checkbox')).find(c => c.textContent!.trim() === 'Sysadmin')!.querySelector('input')!.click();
    const displayName = el().querySelector<HTMLInputElement>('input[name="displayName"]')!;
    displayName.value = 'Private Grif';
    displayName.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    // The edits replaced the model; the loaded object was not changed.
    expect(component.model()).not.toBe(before);
    expect(before.roles[0].hasRole).toBeFalse();
    expect(before.displayName).toBe('Grif');

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

  it('fills the form once the user arrives, without a manual detectChanges', async () => {
    await fixture.whenStable();
    http.expectOne('/api/usermanagement/getuser/15').flush(structuredClone(user));
    await fixture.whenStable();

    expect(el().querySelector('h2')?.textContent).toContain('Edit User 15');
    expect(el().querySelector<HTMLInputElement>('input[name="userName"]')?.value).toBe('grif');
  });

  it('loads lockoutEnd as a DateTime and sends it back unchanged when untouched', async () => {
    fixture.detectChanges();
    http.expectOne('/api/usermanagement/getuser/15').flush({ ...structuredClone(user), lockoutEnd: '2026-03-04T05:06:00+00:00' });
    await fixture.whenStable();
    expect(component.model().lockoutEnd instanceof DateTime).toBeTrue();

    spyOn(console, 'log');
    component.onSubmit();
    const req = http.expectOne('/api/usermanagement/edituser/');
    expect(req.request.body.lockoutEnd).toBe('2026-03-04T05:06:00+00:00');
    req.flush({});
  });

  it('sends an untouched lockout back exactly as loaded, even a permanent one', async () => {
    // DateTimeOffset.MaxValue, as the API sends it; any re-serialization would change it.
    const permanent = '9999-12-31T23:59:59.9999999+00:00';
    fixture.detectChanges();
    http.expectOne('/api/usermanagement/getuser/15').flush({ ...structuredClone(user), lockoutEnd: permanent });
    await fixture.whenStable();
    component.patch({ displayName: 'Renamed' });

    spyOn(console, 'log');
    component.onSubmit();
    const body = http.expectOne('/api/usermanagement/edituser/').request.body;
    expect(body.lockoutEnd).toBe(permanent);
    expect(body.displayName).toBe('Renamed');
  });

  it('sends a changed lockout as ISO 8601 in UTC', async () => {
    fixture.detectChanges();
    http.expectOne('/api/usermanagement/getuser/15').flush({ ...structuredClone(user), lockoutEnd: null });
    await fixture.whenStable();
    component.patch({ lockoutEnd: DateTime.fromISO('2026-05-06T07:08:00+02:00') });

    spyOn(console, 'log');
    component.onSubmit();
    expect(http.expectOne('/api/usermanagement/edituser/').request.body.lockoutEnd).toBe('2026-05-06T05:08:00.000Z');
  });

  it('sends a cleared lockout as null', async () => {
    fixture.detectChanges();
    http.expectOne('/api/usermanagement/getuser/15').flush({ ...structuredClone(user), lockoutEnd: '2026-03-04T05:06:00+00:00' });
    await fixture.whenStable();
    component.patch({ lockoutEnd: null });

    spyOn(console, 'log');
    component.onSubmit();
    expect(http.expectOne('/api/usermanagement/edituser/').request.body.lockoutEnd).toBeNull();
  });
});
