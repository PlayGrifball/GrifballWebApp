import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ToolbarComponent } from './toolbar.component';
import { ThemeComponent } from '../theme/theme.component';
import { AccountService } from '../account.service';
import { Router, ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { ApiClientService } from '../api/apiClient.service';
import { MatSidenav } from '@angular/material/sidenav';
import { of, Subject } from 'rxjs';
import { DateTime } from 'luxon';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('ToolbarComponent', () => {
  let component: ToolbarComponent;
  let fixture: ComponentFixture<ToolbarComponent>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockDialog: jasmine.SpyObj<MatDialog>;
  let mockApiClient: jasmine.SpyObj<ApiClientService>;
  let mockSidenav: jasmine.SpyObj<MatSidenav>;
  let mockActivatedRoute: jasmine.SpyObj<ActivatedRoute>;

  beforeEach(async () => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['logout'], {
      isLoggedIn: signal(false),
      displayName: signal(null),
      personID: signal(null),
    });
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockDialog = jasmine.createSpyObj('MatDialog', ['open']);
    mockApiClient = jasmine.createSpyObj('ApiClientService', ['commitHash', 'commitDate']);
    mockSidenav = jasmine.createSpyObj('MatSidenav', ['toggle']);
    mockActivatedRoute = jasmine.createSpyObj('ActivatedRoute', [], { snapshot: {} });

    // Set up default return values
    mockApiClient.commitHash.and.returnValue(of('abc123'));
    mockApiClient.commitDate.and.returnValue(of(DateTime.now()));

    await TestBed.configureTestingModule({
      imports: [ToolbarComponent, NoopAnimationsModule],
      providers: [
        { provide: AccountService, useValue: mockAccountService },
        { provide: Router, useValue: mockRouter },
        { provide: MatDialog, useValue: mockDialog },
        { provide: ApiClientService, useValue: mockApiClient },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();

    fixture = createToolbar();
    component = fixture.componentInstance;
  });

  function createToolbar(): ComponentFixture<ToolbarComponent> {
    const f = TestBed.createComponent(ToolbarComponent);
    f.componentRef.setInput('snav', mockSidenav);
    return f;
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load commit hash on creation', () => {
    const testHash = 'test-commit-hash';
    mockApiClient.commitHash.and.returnValue(of(testHash));

    component = createToolbar().componentInstance;

    expect(mockApiClient.commitHash).toHaveBeenCalled();
    expect(component.commitHash()).toBe(testHash);
  });

  it('should load commit date on creation', () => {
    const testDate = DateTime.fromISO('2024-01-15T10:30:00Z');
    mockApiClient.commitDate.and.returnValue(of(testDate));

    component = createToolbar().componentInstance;

    expect(mockApiClient.commitDate).toHaveBeenCalled();
    expect(component.commitDate()).toEqual(testDate);
  });

  it('should have null commit hash and date until the API answers', () => {
    mockApiClient.commitHash.and.returnValue(new Subject<string>());
    mockApiClient.commitDate.and.returnValue(new Subject<DateTime>());

    component = createToolbar().componentInstance;

    expect(component.commitHash()).toBeNull();
    expect(component.commitDate()).toBeNull();
  });

  it('renders the commit hash once it arrives, without a manual detectChanges', async () => {
    const hash = new Subject<string>();
    mockApiClient.commitHash.and.returnValue(hash);
    fixture = createToolbar();
    fixture.autoDetectChanges();
    await fixture.whenStable();

    hash.next('deadbeef');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('deadbeef');
  });

  it('toggles the sidenav from the menu button', () => {
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button[aria-label="menu icon"]')!.click();

    expect(mockSidenav.toggle).toHaveBeenCalled();
  });

  it('should have account service injected', () => {
    expect(component.accountService).toBe(mockAccountService);
  });

  it('opens the theme editor as a borderless dialog', () => {
    // The component imports MatDialogModule, so it gets its own MatDialog rather than the mock.
    const open = spyOn(component['dialog'], 'open');
    component.theme();
    expect(open).toHaveBeenCalledWith(ThemeComponent, {
      maxWidth: '100%',
      backdropClass: 'no-backdrop',
      maxHeight: '90vh',
      panelClass: 'dialog-no-bg'
    });
  });
});
