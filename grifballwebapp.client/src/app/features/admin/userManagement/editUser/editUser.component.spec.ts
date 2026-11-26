import { DateTime } from 'luxon';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { EditUserComponent } from './editUser.component';
import { UserResponseDto } from '../userResponseDto';

describe('EditUserComponent', () => {
  let fixture: ComponentFixture<EditUserComponent>;
  let component: EditUserComponent;
  let http: HttpTestingController;

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
    roles: [{ roleName: 'Sysadmin', hasRole: false }, { roleName: 'Commissioner', hasRole: true }], regionID: null
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditUserComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideLuxonDatetimeAdapter(),
        provideRouter([]),
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(EditUserComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('userID', '15');
  });

  afterEach(() => {
    // The linked-accounts section loads its own data once the user arrives; specs that don't look at it leave it unanswered.
    http.match('/api/users/15/external-logins').forEach(req => req.flush({ hasPassword: true, logins: [] }));
    http.verify();
  });

  async function load(): Promise<void> {
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush(structuredClone(user));
    await fixture.whenStable();
    fixture.detectChanges();
    http.expectOne('/api/users/15/external-logins').flush({ hasPassword: true, logins: [{ loginProvider: 'Discord', providerKey: '111', providerDisplayName: 'Discord' }] });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;

  it('does not fetch anything for user id 0, and offers no form', async () => {
    fixture.componentRef.setInput('userID', '0');
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectNone(() => true);
    expect(el().textContent).toContain('No such user.');
    expect(el().querySelector('form')).toBeNull();
  });

  it('holds the form with a skeleton while the user loads', () => {
    fixture.detectChanges();
    expect(el().querySelector('app-skeleton')).not.toBeNull();
    expect(el().querySelector('form')).toBeNull();
    http.expectOne('/api/users/15').flush(structuredClone(user));
  });

  it('loads the user and renders a checkbox per role', async () => {
    await load();

    expect(el().querySelector('h1')!.textContent).toBe('Edit User 15');
    expect(component.model().userName).toBe('grif');
    const labels = Array.from(el().querySelectorAll('mat-checkbox')).map(c => c.textContent!.trim());
    expect(labels).toEqual(['Lockout Enabled', 'Is Dummy User', 'Sysadmin', 'Commissioner']);
  });

  it('logs a failed load, and says so with no form that would save an empty user', async () => {
    const log = spyOn(console, 'error');
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush('x', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(log).toHaveBeenCalled();
    expect(el().querySelector('[role="alert"]')!.textContent).toContain("The user couldn't be loaded.");
    expect(el().querySelector('form')).toBeNull();
  });

  it('posts the edited user, including role changes', async () => {
    await load();
    spyOn(console, 'info');
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
    const req = http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url));
    expect(req.request.method).toBe('POST');
    expect(req.request.body.displayName).toBe('Private Grif');
    expect(req.request.body.roles).toEqual([{ roleName: 'Sysadmin', hasRole: true }, { roleName: 'Commissioner', hasRole: true }]);
    req.flush({});
    expect(console.info).toHaveBeenCalledWith('Updated user 15');
  });

  it('logs a failed update', async () => {
    await load();
    const info = spyOn(console, 'info');
    const log = spyOn(console, 'error');
    component.onSubmit();
    http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url)).flush('x', { status: 400, statusText: 'Bad Request' });
    expect(log).toHaveBeenCalledWith('Failed to update the user', jasmine.anything());
    expect(info).not.toHaveBeenCalled();
  });

  it("lists the user's external logins and removes one", async () => {
    await load();
    expect(el().textContent).toContain('Linked accounts');
    expect(el().querySelector('.linked-provider')!.textContent).toBe('Discord');

    (el().querySelector('.linked-login button') as HTMLButtonElement).click();
    const req = http.expectOne('/api/users/15/external-logins/remove');
    expect(req.request.body).toEqual({ loginProvider: 'Discord', providerKey: '111' });
    req.flush(null);
    http.expectOne('/api/users/15/external-logins').flush({ hasPassword: true, logins: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el().textContent).toContain('Unlinked Discord');
    expect(el().textContent).toContain('No linked accounts.');
  });

  it('resets the form to the new user, with their own linked accounts, when the route moves to another user', async () => {
    await load();
    component.patch({ userName: 'unsaved edit' });

    fixture.componentRef.setInput('userID', '16');
    fixture.detectChanges();
    // Nothing of user 15 while user 16 loads, nor their linked accounts
    expect(component.model().userName).toBeUndefined();
    expect(el().textContent).not.toContain('Linked accounts');

    http.expectOne('/api/users/16').flush({ ...structuredClone(user), userID: 16, userName: 'simmons', lockoutEnd: '2026-03-04T05:06:00+00:00' });
    await fixture.whenStable();
    fixture.detectChanges();
    http.expectOne('/api/users/16/external-logins').flush({ hasPassword: true, logins: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el().querySelector('h1')?.textContent).toContain('Edit User 16');
    expect(el().querySelector<HTMLInputElement>('input[name="userName"]')?.value).toBe('simmons');
    expect(el().textContent).toContain('No linked accounts.');
    // The new user's lockout is the one sent back untouched
    component.onSubmit();
    const save = http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url));
    expect(save.request.body.lockoutEnd).toBe('2026-03-04T05:06:00+00:00');
    save.flush(null);
  });

  it("cancels the previous user's request when the route moves on", async () => {
    fixture.detectChanges();
    const late = http.expectOne('/api/users/15');

    fixture.componentRef.setInput('userID', '16');
    fixture.detectChanges();
    expect(late.cancelled).toBeTrue();
    http.expectOne('/api/users/16').flush({ ...structuredClone(user), userID: 16, userName: 'simmons' });
    await fixture.whenStable();

    expect(component.model().userName).toBe('simmons');
    fixture.detectChanges();
    http.expectOne('/api/users/16/external-logins').flush({ hasPassword: true, logins: [] });
  });

  it('fills the form once the user arrives, without a manual detectChanges after it', async () => {
    // Not whenStable before the answer: a resource still loading keeps the app unstable
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush(structuredClone(user));
    await fixture.whenStable();

    expect(el().querySelector('h1')?.textContent).toContain('Edit User 15');
    expect(el().querySelector<HTMLInputElement>('input[name="userName"]')?.value).toBe('grif');
  });

  it('loads lockoutEnd as a DateTime and sends it back unchanged when untouched', async () => {
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush({ ...structuredClone(user), lockoutEnd: '2026-03-04T05:06:00+00:00' });
    await fixture.whenStable();
    expect(component.model().lockoutEnd instanceof DateTime).toBeTrue();

    spyOn(console, 'error');
    component.onSubmit();
    const req = http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url));
    expect(req.request.body.lockoutEnd).toBe('2026-03-04T05:06:00+00:00');
    req.flush({});
  });

  it('sends an untouched lockout back exactly as loaded, even a permanent one', async () => {
    // DateTimeOffset.MaxValue, as the API sends it; any re-serialization would change it.
    const permanent = '9999-12-31T23:59:59.9999999+00:00';
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush({ ...structuredClone(user), lockoutEnd: permanent });
    await fixture.whenStable();
    component.patch({ displayName: 'Renamed' });

    spyOn(console, 'error');
    component.onSubmit();
    const body = http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url)).request.body;
    expect(body.lockoutEnd).toBe(permanent);
    expect(body.displayName).toBe('Renamed');
  });

  it('sends the original string when the same instant is picked again', async () => {
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush({ ...structuredClone(user), lockoutEnd: '2026-03-04T05:06:07.1234567+00:00' });
    await fixture.whenStable();
    // A new DateTime for the same instant, in another zone, as a picker might hand back.
    component.patch({ lockoutEnd: component.model().lockoutEnd!.setZone('Europe/Berlin') });

    spyOn(console, 'error');
    component.onSubmit();
    expect(http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url)).request.body.lockoutEnd).toBe('2026-03-04T05:06:07.1234567+00:00');
  });

  it('sends a changed lockout as ISO 8601 in UTC', async () => {
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush({ ...structuredClone(user), lockoutEnd: null });
    await fixture.whenStable();
    component.patch({ lockoutEnd: DateTime.fromISO('2026-05-06T07:08:00+02:00') });

    spyOn(console, 'error');
    component.onSubmit();
    expect(http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url)).request.body.lockoutEnd).toBe('2026-05-06T05:08:00.000Z');
  });

  it('sends a cleared lockout as null', async () => {
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush({ ...structuredClone(user), lockoutEnd: '2026-03-04T05:06:00+00:00' });
    await fixture.whenStable();
    component.patch({ lockoutEnd: null });

    spyOn(console, 'error');
    component.onSubmit();
    expect(http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url)).request.body.lockoutEnd).toBeNull();
  });

  it('says there is no such user (204), with no form to save one', async () => {
    fixture.detectChanges();
    http.expectOne('/api/users/15').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    expect(el().textContent).toContain('No such user.');
    expect(el().querySelector('form')).toBeNull();
  });

  it("edits the region, which the import then keeps", async () => {
    await load();
    const select = el().querySelector<HTMLSelectElement>("#eu-region")!;
    expect(el().querySelector("label[for=\"eu-region\"]")).not.toBeNull();
    expect(select.selectedOptions[0].textContent!.trim()).toBe("Not set");
    select.value = "7";
    select.dispatchEvent(new Event("change"));
    expect(component.model().regionID).toBe(7);
    select.value = "";
    select.dispatchEvent(new Event("change"));
    expect(component.model().regionID).toBeNull();
    component.setRegion("3");
    spyOn(console, 'error');
    component.onSubmit();
    const req = http.expectOne(r => r.method === 'POST' && /^\/api\/users\/\d+$/.test(r.url));
    expect(req.request.body.regionID).toBe(3);
    req.flush({});
  });
});
