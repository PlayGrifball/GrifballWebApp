import { Component, Input } from '@angular/core';
import { MediaMatcher } from '@angular/cdk/layout';
import { of } from 'rxjs';
import { ToolbarComponent } from './toolbar/toolbar.component';
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
@Component({ selector: 'app-toolbar', template: '<ng-content />' })
class StubToolbarComponent {
  @Input() snav: unknown;
}

describe('AppComponent behaviour', () => {
  let fixture: ComponentFixture<AppComponent>;
  let component: AppComponent;
  let api: jasmine.SpyObj<ApiClientService>;
  let theming: any;
  let account: any;
  let mobile: boolean;
  let toolbarWidth: number;
  let linkWidth: number;
  let hiddenMissing: boolean;
  let resizeHandler: (() => void) | undefined;

  beforeEach(async () => {
    mobile = false;
    toolbarWidth = 250;
    linkWidth = 100;
    hiddenMissing = false;

    account = {
      isLoggedIn: signal(false),
      isEventOrganizer: signal(false),
      isSysAdmin: signal(false),
      isPlayer: signal(false),
      personID: signal(null),
      displayName: signal(null)
    };
    api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID']);
    api.getCurrentSeasonID.and.returnValue(of(0));
    theming = {
      neutral: signal('#111111'),
      neutralVariant: signal('#222222'),
      error: signal('#ff0000'),
      font: signal('Roboto'),
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

    const media = {
      matchMedia: () => ({
        get matches() { return mobile; },
        addEventListener: () => {},
        removeEventListener: () => {},
      })
    };

    // Capture the resize handler instead of registering it on the real window: the component
    // never removes it, so a real listener would outlive this test.
    resizeHandler = undefined;
    const realAdd = window.addEventListener.bind(window);
    spyOn(window, "addEventListener").and.callFake(((type: string, fn: any, opts?: any) =>
      type === "resize" ? (resizeHandler = fn) : realAdd(type, fn, opts)) as any);

    // The layout code measures DOM elements; stub the two lookups so widths are deterministic.
    const realQuery = document.querySelector.bind(document);
    spyOn(document, 'querySelector').and.callFake(((sel: string) =>
      sel === '.toolbar-links' ? { clientWidth: toolbarWidth } : realQuery(sel)) as any);
    const realById = document.getElementById.bind(document);
    spyOn(document, 'getElementById').and.callFake(((id: string) =>
      id === 'hidden' ? (hiddenMissing ? null : { innerText: '', offsetWidth: linkWidth }) : realById(id)) as any);

    await TestBed.configureTestingModule({
      imports: [AppComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: AccountService, useValue: account },
        { provide: ApiClientService, useValue: api },
        { provide: ThemingService, useValue: theming },
        { provide: MediaMatcher, useValue: media },
      ]
    })
      .overrideComponent(AppComponent, {
        remove: { imports: [ToolbarComponent] },
        add: { imports: [StubToolbarComponent] }
      })
      .compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
  });

  it('applies every theming signal through the theming service', () => {
    fixture.detectChanges();

    expect(theming.setFont).toHaveBeenCalledWith('Roboto');
    expect(theming.setPrimaryShade).toHaveBeenCalledWith('#0000ff');
    expect(theming.setSecondaryShade).toHaveBeenCalledWith('#00ff00');
    expect(theming.setTertiaryShade).toHaveBeenCalledWith('#ff00ff');
    expect(theming.setNeutralShade).toHaveBeenCalledWith('#111111');
    expect(theming.setNeutralVariantShade).toHaveBeenCalledWith('#222222');
    expect(theming.setErrorShade).toHaveBeenCalledWith('#ff0000');
  });

  it('re-applies the font when the theming signal changes', () => {
    fixture.detectChanges();
    theming.font.set('sans-serif');
    fixture.detectChanges();

    expect(theming.setFont).toHaveBeenCalledWith('sans-serif');
  });

  it('does not add a Current Season link when there is no current season', () => {
    fixture.detectChanges();

    expect(component.navLinks.map(n => n.title)).not.toContain('Current Season');
  });

  it('adds a Current Season link pointing at the current season', () => {
    api.getCurrentSeasonID.and.returnValue(of(12));
    fixture.detectChanges();

    const current = component.navLinks.find(n => n.title === 'Current Season');
    expect(current?.path).toBe('/season/12');
    expect(current?.show()).toBeTrue();
  });

  it('shows organizer and admin links only to those roles', () => {
    const show = (title: string) => component.navLinks.find(n => n.title === title)!.show();
    expect(show('Home')).toBeTrue();
    expect(show('Season Manager')).toBeFalse();
    expect(show('User Management')).toBeFalse();

    account.isEventOrganizer.set(true);
    expect(show('Season Manager')).toBeTrue();
    expect(show('Commissioner Dashboard')).toBeTrue();
    expect(show('Excel Exporter')).toBeTrue();
    expect(show('User Management')).toBeFalse();

    account.isSysAdmin.set(true);
    expect(show('User Management')).toBeTrue();
    expect(show('Infinite Client')).toBeTrue();
  });

  it('fits as many links as the toolbar width allows and overflows the rest', () => {
    fixture.detectChanges();

    // 250px toolbar, 100px links: two fit (100, 200), the third would need 300.
    expect(component.visibleNavLinks.map(n => n.title)).toEqual(['Home', 'Late League']);
    expect(component.overflowNavLinks.length).toBe(component.navLinks.length - 2);
  });

  it('renders the visible links in the toolbar and all permitted links in the sidenav', () => {
    fixture.detectChanges();
    const toolbarLinks = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.toolbar-links a:not(#hidden)'))
      .map(a => a.textContent!.trim());
    expect(toolbarLinks).toEqual(['Home', 'Late League']);
  });

  it('puts every link in the overflow on a mobile-width screen', () => {
    mobile = true;
    fixture.detectChanges();

    expect(component.visibleNavLinks).toEqual([]);
    expect(component.overflowNavLinks.length).toBe(component.navLinks.length);
  });

  it('recomputes the layout when the window is resized', () => {
    fixture.detectChanges();
    toolbarWidth = 10000;
    expect(resizeHandler).toBeDefined();
    resizeHandler!();

    expect(component.visibleNavLinks.length).toBe(component.navLinks.length);
    expect(component.overflowNavLinks).toEqual([]);
  });

  it('logs and treats links as zero width when the measuring element is missing', () => {
    hiddenMissing = true;
    const err = spyOn(console, 'error');
    fixture.detectChanges();

    expect(err).toHaveBeenCalledWith('Hidden link element not found!');
    expect(component.visibleNavLinks.length).toBe(component.navLinks.length);
  });
});
