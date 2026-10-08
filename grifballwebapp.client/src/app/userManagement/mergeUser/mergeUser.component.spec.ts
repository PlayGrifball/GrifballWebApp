import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MergeUserComponent } from './mergeUser.component';
import { UserResponseDto } from '../userResponseDto';

describe('MergeUserComponent', () => {
  let fixture: ComponentFixture<MergeUserComponent>;
  let component: MergeUserComponent;
  let httpMock: HttpTestingController;

  const user = (id: number): UserResponseDto => ({
    userID: id, userName: 'user' + id, lockoutEnd: null, lockoutEnabled: false, isDummyUser: false,
    accessFailedCount: 0, region: null, displayName: 'User ' + id, gamertag: null, discord: null,
    externalAuthCount: 0, hasPassword: true, roles: [],
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const mergeButton = () => el().querySelector('button[mat-flat-button]') as HTMLButtonElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MergeUserComponent, NoopAnimationsModule],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
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
    httpMock.expectOne('/api/usermanagement/getuser/1').flush(user(1));
    httpMock.expectOne('/api/usermanagement/getuser/2').flush(user(2));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el().textContent).toContain('user1');
    expect(el().textContent).toContain('user2');
    expect(mergeButton().disabled).toBeFalse();
  });

  it('does not throw when the API fails and shows an error with merge disabled', async () => {
    fixture.componentRef.setInput('fromInput', 1);
    fixture.componentRef.setInput('toInput', 2);
    fixture.detectChanges();
    httpMock.expectOne('/api/usermanagement/getuser/1').flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/usermanagement/getuser/2').flush(user(2));
    await fixture.whenStable();

    expect(component.mergeFrom.status()).toBe('error');
    expect(() => fixture.detectChanges()).not.toThrow();

    const alerts = el().querySelectorAll('[role="alert"]');
    expect(alerts.length).toBe(1);
    expect(alerts[0].textContent).toContain('Failed to load user');
    expect(el().textContent).toContain('user2');
    expect(mergeButton().disabled).toBeTrue();
  });
});
