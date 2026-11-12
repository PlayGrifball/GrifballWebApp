import { DateTime } from 'luxon';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MergeUserComponent } from './mergeUser.component';
import { UserResponseDto } from '../userResponseDto';
import { noMaterialAnimations } from '../../testing/noMaterialAnimations';

// A user with nothing set beyond its ID and the names made from it
const user = (id: number): UserResponseDto => ({
  userID: id, userName: 'user' + id, lockoutEnd: null, lockoutEnabled: false, isDummyUser: false,
  accessFailedCount: 0, region: null, displayName: 'User ' + id, gamertag: null, discord: null,
  externalAuthCount: 0, hasPassword: true, roles: [],
});

describe('MergeUserComponent', () => {
  let fixture: ComponentFixture<MergeUserComponent>;
  let component: MergeUserComponent;
  let httpMock: HttpTestingController;

  const el = () => fixture.nativeElement as HTMLElement;
  const mergeButton = () => el().querySelector('button[mat-flat-button]') as HTMLButtonElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MergeUserComponent],
      providers: [noMaterialAnimations, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MergeUserComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => httpMock.verify());

  it('does not request a user while no ID is selected', async () => {
    expect(() => fixture.detectChanges()).not.toThrow();
    httpMock.expectNone(() => true);
    expect(el().textContent).toContain('User not found.');
    expect(mergeButton().disabled).toBeTrue();
  });

  it('shows both users and enables merge on success', async () => {
    fixture.componentRef.setInput('fromInput', 1);
    fixture.componentRef.setInput('toInput', 2);
    fixture.detectChanges();
    httpMock.expectOne('/api/users/1').flush(user(1));
    httpMock.expectOne('/api/users/2').flush(user(2));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el().textContent).toContain('user1');
    expect(el().textContent).toContain('user2');
    expect(mergeButton().disabled).toBeFalse();
  });

  it('shows "User not found." for an unknown ID (the API answers 404), not an error', async () => {
    fixture.componentRef.setInput('fromInput', 1);
    fixture.componentRef.setInput('toInput', 2);
    fixture.detectChanges();
    httpMock.expectOne('/api/users/1').flush({ status: 404, title: 'Not found', detail: 'There is no such user' }, { status: 404, statusText: 'Not Found' });
    httpMock.expectOne('/api/users/2').flush(user(2));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el().querySelectorAll('[role="alert"]')).toHaveSize(0);
    expect(el().textContent).toContain('User not found.');
    expect(el().textContent).toContain('user2');
    expect(mergeButton().disabled).toBeTrue();
  });

  it('does not throw when the API fails and shows an error with merge disabled', async () => {
    fixture.componentRef.setInput('fromInput', 1);
    fixture.componentRef.setInput('toInput', 2);
    fixture.detectChanges();
    httpMock.expectOne('/api/users/1').flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/users/2').flush(user(2));
    await fixture.whenStable();

    expect(component.mergeFrom.status()).toBe('error');
    expect(() => fixture.detectChanges()).not.toThrow();

    const alerts = el().querySelectorAll('[role="alert"]');
    expect(alerts).toHaveSize(1);
    expect(alerts[0].textContent).toContain('Failed to load user');
    expect(el().textContent).toContain('user2');
    expect(mergeButton().disabled).toBeTrue();
  });
});

describe('MergeUserComponent merging', () => {
  let fixture: ComponentFixture<MergeUserComponent>;
  let component: MergeUserComponent;
  let httpMock: HttpTestingController;

  const mergeButton = () => (fixture.nativeElement as HTMLElement).querySelector('button[mat-flat-button]') as HTMLButtonElement;

  beforeEach(async () => {
    spyOn(console, 'log');
    TestBed.configureTestingModule({
      imports: [MergeUserComponent],
      providers: [noMaterialAnimations, provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MergeUserComponent);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('fromInput', 5);
    fixture.componentRef.setInput('toInput', 6);
    fixture.detectChanges();
    httpMock.expectOne('/api/users/5').flush(user(5));
    httpMock.expectOne('/api/users/6').flush(user(6));
    await fixture.whenStable();
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('will not merge a user into itself', async () => {
    component.mergeToId.set(5);
    fixture.detectChanges();
    httpMock.expectOne('/api/users/5').flush(user(5));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(mergeButton().disabled).toBeTrue();
  });

  it('posts both ids and every merge option, all on by default', () => {
    component.transferXbox.set(false);
    component.deleteMergeFrom.set(false);

    mergeButton().click();
    const req = httpMock.expectOne('/api/user-merges');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      mergeToId: 6,
      mergeFromId: 5,
      mergeOptions: {
        transferDiscord: true,
        transferLogins: true,
        transferXbox: false,
        transferTeamPlayers: true,
        transferExperiences: true,
        transferSignups: true,
        deleteMergeFrom: false,
      },
    });
    req.flush({});
    expect(console.log).toHaveBeenCalledWith('Merged user');
  });

  it('logs a failed merge', () => {
    component.submit();
    httpMock.expectOne('/api/user-merges').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(console.log).not.toHaveBeenCalledWith('Merged user');
  });

  it('disables the button while the merge is in flight, so a double click sends one request', async () => {
    mergeButton().click();
    fixture.detectChanges();
    expect(component.submitting()).toBeTrue();
    expect(mergeButton().disabled).toBeTrue();
    mergeButton().click();

    const req = httpMock.expectOne('/api/user-merges');
    req.flush({});
    fixture.detectChanges();

    expect(component.submitting()).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')!.textContent).toContain('Merged user 5 into 6');
  });

  it('sends transferLogins off when toggled off', () => {
    component.transferLogins.set(false);
    component.submit();
    const req = httpMock.expectOne('/api/user-merges');
    expect(req.request.body.mergeOptions.transferLogins).toBeFalse();
    req.flush({});
  });

  it("shows the server's message when the source is already gone", () => {
    component.submit();
    httpMock.expectOne('/api/user-merges')
      .flush('Did not find user 5. It may already have been merged or deleted.', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')!;
    expect(alert.textContent).toContain('Did not find user 5. It may already have been merged or deleted.');
    expect(mergeButton().disabled).toBeFalse();
  });

  it('falls back to a generic message without one from the server', () => {
    component.submit();
    httpMock.expectOne('/api/user-merges').flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.result()).toEqual({ ok: false, text: 'Merge failed' });
  });

  it('reads the loaded users through parseUser (lockoutEnd as a DateTime)', async () => {
    component.mergeToId.set(7);
    fixture.detectChanges();
    httpMock.expectOne('/api/users/7').flush({ ...user(7), lockoutEnd: '2026-03-04T05:06:00+00:00' });
    await fixture.whenStable();

    expect(component.mergeToUser()?.lockoutEnd instanceof DateTime).toBeTrue();
    expect(component.mergeFromUser()?.lockoutEnd).toBeNull();
  });
});
