import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ToolbarComponent } from './toolbar.component';
import { AccountService } from '../account.service';
import { Router, ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { ApiClientService } from '../api/apiClient.service';
import { MatSidenav } from '@angular/material/sidenav';
import { of } from 'rxjs';
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
    mockAccountService = jasmine.createSpyObj('AccountService', ['logout']);
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

    fixture = TestBed.createComponent(ToolbarComponent);
    component = fixture.componentInstance;
    component.snav = mockSidenav;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load commit hash on init', () => {
    const testHash = 'test-commit-hash';
    mockApiClient.commitHash.and.returnValue(of(testHash));

    component.ngOnInit();

    expect(mockApiClient.commitHash).toHaveBeenCalled();
    expect(component.commitHash).toBe(testHash);
  });

  it('should load commit date on init', () => {
    const testDate = DateTime.fromISO('2024-01-15T10:30:00Z');
    mockApiClient.commitDate.and.returnValue(of(testDate));

    component.ngOnInit();

    expect(mockApiClient.commitDate).toHaveBeenCalled();
    expect(component.commitDate).toEqual(testDate);
  });

  it('should initialize with null commit hash and date', () => {
    expect(component.commitHash).toBeNull();
    expect(component.commitDate).toBeNull();
  });

  it('should have account service injected', () => {
    expect(component.accountService).toBe(mockAccountService);
  });
});
