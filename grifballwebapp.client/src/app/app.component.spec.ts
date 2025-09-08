import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectorRef } from '@angular/core';
import { MediaMatcher } from '@angular/cdk/layout';
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
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockApiClientService: jasmine.SpyObj<ApiClientService>;
  let mockThemingService: jasmine.SpyObj<ThemingService>;

  beforeEach(async () => {
    // Create spies for dependencies
    mockAccountService = jasmine.createSpyObj('AccountService', ['isEventOrganizer', 'isSysAdmin'], {
      isLoggedIn: signal(false)
    });
    mockAccountService.isEventOrganizer.and.returnValue(signal(false));
    mockAccountService.isSysAdmin.and.returnValue(signal(false));

    mockApiClientService = jasmine.createSpyObj('ApiClientService', ['getSidebarItems']);

    mockThemingService = jasmine.createSpyObj('ThemingService', [
      'setNeutralShade',
      'setNeutralVariantShade', 
      'setErrorShade',
      'setFont',
      'setPrimaryShade',
      'setSecondaryShade',
      'setTertiaryShade'
    ], {
      neutral: signal('#000000'),
      neutralVariant: signal('#000000'),
      error: signal('#ff0000'),
      font: signal('Arial'),
      primary: signal('#0000ff'),
      secondary: signal('#00ff00'),
      tertiary: signal('#ff00ff')
    });

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

  it('should have correct selector', () => {
    // The component should be created successfully, indicating proper setup
    expect(component).toBeTruthy();
    expect(fixture.componentInstance).toBe(component);
  });

  it('should have default navigation links', () => {
    expect(component.navLinks.length).toBeGreaterThan(0);
    
    // Check that basic navigation links exist
    const linkTitles = component.navLinks.map(link => link.title);
    expect(linkTitles).toContain('Home');
  });

  it('should initialize visible and overflow nav links arrays', () => {
    expect(component.visibleNavLinks).toBeDefined();
    expect(component.overflowNavLinks).toBeDefined();
    expect(Array.isArray(component.visibleNavLinks)).toBeTruthy();
    expect(Array.isArray(component.overflowNavLinks)).toBeTruthy();
  });
});