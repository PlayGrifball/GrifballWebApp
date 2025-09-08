import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { AppComponent } from './app.component';
import { AccountService } from './account.service';
import { ApiClientService } from './api/apiClient.service';
import { ThemingService } from './theming.service';

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    // Create simple mock services
    const mockAccountService = {
      isLoggedIn: signal(false),
      isEventOrganizer: signal(false),
      isSysAdmin: signal(false),
      isPlayer: signal(false),
      personID: signal(null),
      displayName: signal(null)
    };

    const mockApiClientService = jasmine.createSpyObj('ApiClientService', ['getSidebarItems']);

    const mockThemingService = {
      neutral: signal('#000000'),
      neutralVariant: signal('#000000'),
      error: signal('#ff0000'),
      font: signal('Arial'),
      primary: signal('#0000ff'),
      secondary: signal('#00ff00'),
      tertiary: signal('#ff00ff'),
      setNeutralShade: jasmine.createSpy('setNeutralShade'),
      setNeutralVariantShade: jasmine.createSpy('setNeutralVariantShade'),
      setErrorShade: jasmine.createSpy('setErrorShade'),
      setFont: jasmine.createSpy('setFont'),
      setPrimaryShade: jasmine.createSpy('setPrimaryShade'),
      setSecondaryShade: jasmine.createSpy('setSecondaryShade'),
      setTertiaryShade: jasmine.createSpy('setTertiaryShade')
    };

    await TestBed.configureTestingModule({
      imports: [
        AppComponent,
        NoopAnimationsModule
      ],
      providers: [
        provideRouter([]),
        { provide: AccountService, useValue: mockAccountService },
        { provide: ApiClientService, useValue: mockApiClientService },
        { provide: ThemingService, useValue: mockThemingService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have navigation links', () => {
    expect(component.navLinks).toBeDefined();
    expect(Array.isArray(component.navLinks)).toBeTruthy();
    expect(component.navLinks.length).toBeGreaterThan(0);
  });

  it('should initialize arrays for visible and overflow nav links', () => {
    expect(component.visibleNavLinks).toBeDefined();
    expect(component.overflowNavLinks).toBeDefined();
    expect(Array.isArray(component.visibleNavLinks)).toBeTruthy();
    expect(Array.isArray(component.overflowNavLinks)).toBeTruthy();
  });
});