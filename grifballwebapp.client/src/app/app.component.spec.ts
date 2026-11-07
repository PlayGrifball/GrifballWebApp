import { Component, PLATFORM_ID, input } from '@angular/core';
import { MediaMatcher } from '@angular/cdk/layout';
import { of, Subject } from 'rxjs';
import { ToolbarComponent } from './toolbar/toolbar.component';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { AppComponent, MENU_BUTTON_WIDTH, NAV_LINK_GAP } from './app.component';
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

    // The real toolbar is rendered here and asks for the build's commit hash and date on creation.
    const mockApiClientService = jasmine.createSpyObj('ApiClientService', ['getSidebarItems', 'commitHash', 'commitDate']);
    mockApiClientService.commitHash.and.returnValue(of(''));
    mockApiClientService.commitDate.and.returnValue(of(null));

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
    expect(component.navLinks()).toBeDefined();
    expect(Array.isArray(component.navLinks())).toBeTruthy();
    expect(component.navLinks().length).toBeGreaterThan(0);
  });

  it('should initialize arrays for visible and overflow nav links', () => {
    expect(component.visibleNavLinks()).toBeDefined();
    expect(component.overflowNavLinks()).toBeDefined();
    expect(Array.isArray(component.visibleNavLinks())).toBeTruthy();
    expect(Array.isArray(component.overflowNavLinks())).toBeTruthy();
  });
});
@Component({ selector: 'app-toolbar', template: '<ng-content />' })
class StubToolbarComponent {
  readonly snav = input<unknown>();
  readonly showMenu = input<boolean>();
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
  let mobileListeners: Map<string, Function>;
  let removedMobileListeners: Function[];
  let frames: FrameRequestCallback[];
  let resizeObserved: Element[];
  let resizeObserverCallback: ResizeObserverCallback | undefined;
  let resizeObserverDisconnect: jasmine.Spy;
  let realResizeObserver: typeof ResizeObserver;

  afterEach(() => (window as any).ResizeObserver = realResizeObserver);

  /** The links this user (an organizer, so the bar has more links than fit) may see. */
  const shown = () => component.navLinks().filter(n => n.show()).length;

  const resize = () => {
    resizeHandler!();
    frames.splice(0).forEach(frame => frame(0));
  };

  beforeEach(async () => {
    mobile = false;
    toolbarWidth = 250;
    linkWidth = 100;
    hiddenMissing = false;

    account = {
      isLoggedIn: signal(false),
      isEventOrganizer: signal(true),
      isSysAdmin: signal(false),
      isPlayer: signal(false),
      personID: signal(null),
      displayName: signal(null)
    };
    api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID', 'commitHash', 'commitDate']);
    api.getCurrentSeasonID.and.returnValue(of(0));
    // The footer shows the build.
    api.commitHash.and.returnValue(of(''));
    api.commitDate.and.returnValue(of(null));
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

    mobileListeners = new Map();
    removedMobileListeners = [];
    const media = {
      matchMedia: () => ({
        get matches() { return mobile; },
        addEventListener: (type: string, fn: Function) => mobileListeners.set(type, fn),
        removeEventListener: (type: string, fn: Function) => removedMobileListeners.push(fn),
      })
    };

    // Resizes are measured once per animation frame; the specs run the queued frames by hand.
    frames = [];
    spyOn(window, 'requestAnimationFrame').and.callFake(((cb: FrameRequestCallback) => frames.push(cb)) as any);
    spyOn(window, 'cancelAnimationFrame');

    // The bar's size is watched with a ResizeObserver; the specs call its callback by hand.
    resizeObserved = [];
    resizeObserverCallback = undefined;
    resizeObserverDisconnect = jasmine.createSpy('disconnect');
    realResizeObserver = window.ResizeObserver;
    (window as any).ResizeObserver = class {
      constructor(cb: ResizeObserverCallback) { resizeObserverCallback = cb; }
      observe(el: Element) { resizeObserved.push(el); }
      unobserve() { }
      disconnect() { resizeObserverDisconnect(); }
    };

    // Capture the resize handler instead of registering it on the real window, so a real
    // listener cannot leak out of this test.
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

    expect(component.navLinks().map(n => n.title)).not.toContain('Current Season');
  });

  it('adds a Current Season link pointing at the current season', () => {
    api.getCurrentSeasonID.and.returnValue(of(12));
    fixture.detectChanges();

    const current = component.navLinks().find(n => n.title === 'Current Season');
    expect(current?.path).toBe('/season/12');
    expect(current?.show()).toBeTrue();
  });

  it('shows organizer and admin links only to those roles', () => {
    const show = (title: string) => component.navLinks().find(n => n.title === title)!.show();
    account.isEventOrganizer.set(false);
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
    expect(show('Database Backup')).toBeTrue();
  });

  it('fits as many links as the toolbar width allows and overflows the rest', () => {
    fixture.detectChanges();

    // 250px toolbar, 100px links: two fit (100, 204 with the gap), the third would need 308.
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home', 'Seasons']);
    expect(component.overflowNavLinks().length).toBe(shown() - 2);
  });

  it('renders the visible links in the toolbar and all permitted links in the sidenav', () => {
    fixture.detectChanges();
    const toolbarLinks = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.toolbar-links a:not(#hidden)'))
      .map(a => a.textContent!.trim());
    expect(toolbarLinks).toEqual(['Home', 'Seasons']);
  });

  it('puts every link in the overflow on a mobile-width screen', () => {
    mobile = true;
    fixture.detectChanges();

    expect(component.visibleNavLinks()).toEqual([]);
    expect(component.overflowNavLinks().length).toBe(shown());
  });

  it('recomputes the layout when the window is resized', () => {
    fixture.detectChanges();
    toolbarWidth = 10000;
    expect(resizeHandler).toBeDefined();
    resize();

    expect(component.visibleNavLinks().length).toBe(shown());
    expect(component.overflowNavLinks()).toEqual([]);
  });

  it('logs and treats links as zero width when the measuring element is missing', () => {
    hiddenMissing = true;
    const err = spyOn(console, 'error');
    fixture.detectChanges();

    expect(err).toHaveBeenCalledWith('Hidden link element not found!');
    expect(component.visibleNavLinks().length).toBe(shown());
  });

  it('renders a Current Season link that arrives after the first render without a manual detectChanges', async () => {
    const season = new Subject<number>();
    api.getCurrentSeasonID.and.returnValue(season);
    fixture.autoDetectChanges();
    await fixture.whenStable();

    season.next(7);
    await fixture.whenStable();

    const sidenavLinks = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('mat-sidenav a'))
      .map(a => a.textContent!.trim());
    expect(sidenavLinks).toContain('Current Season');
  });

  it('shows a Current Season link that arrives after the first render in the toolbar, without a resize', async () => {
    toolbarWidth = 10000;
    const season = new Subject<number>();
    api.getCurrentSeasonID.and.returnValue(season);
    fixture.autoDetectChanges();
    await fixture.whenStable();

    season.next(7);
    await fixture.whenStable();

    expect(component.visibleNavLinks().map(n => n.title)).toContain('Current Season');
    const toolbarLinks = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.toolbar-links a:not(#hidden)'))
      .map(a => a.textContent!.trim());
    expect(toolbarLinks).toContain('Current Season');
  });

  it('moves the links to the overflow when a resize switches to the mobile layout', () => {
    fixture.detectChanges();
    expect(component.visibleNavLinks().length).toBe(2);

    mobile = true;
    resize();

    expect(component.visibleNavLinks()).toEqual([]);
    expect(component.overflowNavLinks().length).toBe(shown());
  });

  it('lays out the links from measured widths without touching the DOM while computing', () => {
    fixture.detectChanges();
    resize(); // measures, and invalidates the layout
    (document.getElementById as jasmine.Spy).calls.reset();
    (document.querySelector as jasmine.Spy).calls.reset();

    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home', 'Seasons']);
    expect(document.getElementById).not.toHaveBeenCalled();
    expect(document.querySelector).not.toHaveBeenCalled();
  });

  it('measures at most once per animation frame however many resize events come', () => {
    fixture.detectChanges();
    toolbarWidth = 10000;
    const queued = frames.length;
    resizeHandler!();
    resizeHandler!();
    resizeHandler!();
    expect(frames.length).toBe(queued + 1);
    expect(component.visibleNavLinks().length).toBe(2); // not measured yet

    frames.splice(0).forEach(frame => frame(0));
    expect(component.visibleNavLinks().length).toBe(shown());
  });

  it('re-measures when the mobile breakpoint is crossed, and stops listening once destroyed', () => {
    fixture.detectChanges();
    const onChange = mobileListeners.get('change')!;
    expect(onChange).toBeDefined();

    mobile = true;
    onChange();
    expect(component.visibleNavLinks()).toEqual([]);

    resizeHandler!(); // a frame is pending
    fixture.destroy();
    expect(removedMobileListeners).toContain(onChange);
    expect(window.cancelAnimationFrame).toHaveBeenCalled();
  });

  it('stops listening for resizes once destroyed', () => {
    const remove = spyOn(window, 'removeEventListener').and.callThrough();
    fixture.detectChanges();
    fixture.destroy();

    expect(remove).toHaveBeenCalledWith('resize', resizeHandler!);
  });

  it('needs no menu button when every link fits', () => {
    toolbarWidth = 10000;
    fixture.detectChanges();
    expect(component.overflowNavLinks()).toEqual([]);
    expect(component.showMenu()).toBeFalse();
  });

  it('leaves room for the menu button once some links overflow', () => {
    // The bar is measured as rendered, with the menu button (all links overflow before the first
    // measurement): 152px plus the button is 200px, too narrow for the two 100px links (each must
    // fit strictly), so the button stays and one link fits next to it.
    toolbarWidth = 200 - MENU_BUTTON_WIDTH;
    fixture.detectChanges();
    expect(component.showMenu()).toBeTrue();
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home']);

    // Measured again: the same layout, it does not flip back and forth.
    resize();
    expect(component.showMenu()).toBeTrue();
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home']);
  });

  it('counts the gaps between the links, so the last one is never cut off', () => {
    account.isEventOrganizer.set(false);
    // Four 100px links: 400px without the gaps between them, 412px with them. A 405px bar (measured
    // with the menu button, as all links overflow before the first measurement) can't take all four.
    toolbarWidth = 405 - MENU_BUTTON_WIDTH;
    fixture.detectChanges();
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home', 'Seasons', 'Leaderboards']);
    expect(component.overflowNavLinks().map(n => n.title)).toEqual(['Late League']);
  });

  it('fits every link, and shows no menu, when the bar has room for the gaps too', () => {
    account.isEventOrganizer.set(false);
    toolbarWidth = 400 + 3 * NAV_LINK_GAP + 1 - MENU_BUTTON_WIDTH;
    fixture.detectChanges();
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home', 'Seasons', 'Leaderboards', 'Late League']);
    expect(component.showMenu()).toBeFalse();

    // Measured again without the menu button: the same.
    toolbarWidth = 400 + 3 * NAV_LINK_GAP + 1;
    resize();
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home', 'Seasons', 'Leaderboards', 'Late League']);
  });

  it('gives links hidden from this user no room in the bar', () => {
    account.isEventOrganizer.set(false);
    toolbarWidth = 150;
    fixture.detectChanges();
    // Home fits and Seasons, Leaderboards and Late League (everyone's) overflow; the organizer and admin links are neither shown nor counted.
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home']);
    expect(component.overflowNavLinks().map(n => n.title)).toEqual(['Seasons', 'Leaderboards', 'Late League']);
  });

  it('has no Login link: the header\'s Login button (or the user\'s name) is the way in', () => {
    fixture.detectChanges();
    expect(component.navLinks().map(n => n.title)).not.toContain('Login');
    expect(component.navLinks().map(n => n.path)).not.toContain('/login');
  });

  it('puts the Current Season link right after Home', () => {
    api.getCurrentSeasonID.and.returnValue(of(4));
    fixture.detectChanges();
    expect(component.navLinks().map(n => n.title).slice(0, 3)).toEqual(['Home', 'Current Season', 'Seasons']);
  });

  it('measures again once the web fonts have loaded', async () => {
    fixture.detectChanges();
    expect(component.visibleNavLinks().length).toBe(2);
    toolbarWidth = 10000;
    await document.fonts.ready;
    await Promise.resolve();
    expect(component.visibleNavLinks().length).toBe(shown());
  });

  it('does not measure after being destroyed, even when the fonts load later', async () => {
    fixture.detectChanges();
    fixture.destroy();
    toolbarWidth = 10000;
    await document.fonts.ready;
    await Promise.resolve();
    expect(component.visibleNavLinks().length).toBe(2);
  });

  // #307: signing in swaps the Login button for the wider user chip without a window resize, so links
  // laid out for the signed-out bar were cut off.
  it('measures the bar again when signing in narrows it, so no link is cut off', async () => {
    account.isEventOrganizer.set(false);
    toolbarWidth = 10000;
    fixture.autoDetectChanges();
    await fixture.whenStable();
    expect(component.visibleNavLinks().length).toBe(shown());

    // The user chip leaves 150px: room for Home beside the menu button, which now shows.
    toolbarWidth = 150;
    account.isLoggedIn.set(true);
    await fixture.whenStable();
    expect(component.showMenu()).toBeTrue();
    expect(component.visibleNavLinks().map(n => n.title)).toEqual(['Home']);

    // The user's name arriving later narrows it again.
    toolbarWidth = 50;
    account.displayName.set('Commander Grif');
    await fixture.whenStable();
    expect(component.visibleNavLinks()).toEqual([]);
  });

  it('measures the bar again when the links gain room on signing out', async () => {
    account.isLoggedIn.set(true);
    toolbarWidth = 150;
    fixture.autoDetectChanges();
    await fixture.whenStable();
    expect(component.showMenu()).toBeTrue();

    toolbarWidth = 10000;
    account.isLoggedIn.set(false);
    await fixture.whenStable();
    expect(component.visibleNavLinks().length).toBe(shown());
    expect(component.showMenu()).toBeFalse();
  });

  it('watches the bar for size changes that come without a window resize, once a frame', () => {
    fixture.detectChanges();
    expect(resizeObserved).toEqual([(fixture.nativeElement as HTMLElement).querySelector('.toolbar-links')!]);
    expect(component.visibleNavLinks().length).toBe(2);

    // The user's name, a font or a scrollbar changes the bar's width.
    toolbarWidth = 10000;
    const queued = frames.length;
    resizeObserverCallback!([], {} as ResizeObserver);
    resizeObserverCallback!([], {} as ResizeObserver);
    expect(frames.length).toBe(queued + 1);
    frames.splice(0).forEach(frame => frame(0));
    expect(component.visibleNavLinks().length).toBe(shown());
  });

  it('stops watching the bar once destroyed', () => {
    fixture.detectChanges();
    fixture.destroy();
    expect(resizeObserverDisconnect).toHaveBeenCalled();
  });

  it('still lays out the links, on window resizes, where the browser has no ResizeObserver', () => {
    (window as any).ResizeObserver = undefined;
    fixture.detectChanges();
    expect(resizeObserved).toEqual([]);
    toolbarWidth = 10000;
    resize();
    expect(component.visibleNavLinks().length).toBe(shown());
    expect(() => fixture.destroy()).not.toThrow();
  });

  it('still lays out the links where the browser has no font loading API', () => {
    spyOnProperty(document, 'fonts', 'get').and.returnValue(undefined as unknown as FontFaceSet);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.visibleNavLinks().length).toBe(2);
  });
});

describe('AppComponent rendered on the server', () => {
  it('leaves the window, its size and the theme alone', () => {
    const theming = {
      neutral: signal('#000000'), neutralVariant: signal('#000000'), error: signal('#ff0000'), font: signal('Arial'),
      primary: signal('#0000ff'), secondary: signal('#00ff00'), tertiary: signal('#ff00ff'),
      setNeutralShade: jasmine.createSpy(), setNeutralVariantShade: jasmine.createSpy(), setErrorShade: jasmine.createSpy(),
      setFont: jasmine.createSpy('setFont'), setPrimaryShade: jasmine.createSpy(), setSecondaryShade: jasmine.createSpy(), setTertiaryShade: jasmine.createSpy(),
    };
    const api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID', 'commitHash', 'commitDate']);
    api.getCurrentSeasonID.and.returnValue(of(0));
    api.commitHash.and.returnValue(of(''));
    api.commitDate.and.returnValue(of(null));
    const addListener = spyOn(window, 'addEventListener').and.callThrough();
    const removeListener = spyOn(window, 'removeEventListener').and.callThrough();
    TestBed.configureTestingModule({
      imports: [AppComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'server' },
        { provide: AccountService, useValue: { isLoggedIn: signal(false), isEventOrganizer: signal(false), isSysAdmin: signal(false), isPlayer: signal(false), personID: signal(null), displayName: signal(null) } },
        { provide: ApiClientService, useValue: api },
        { provide: ThemingService, useValue: theming },
      ]
    }).overrideComponent(AppComponent, {
      remove: { imports: [ToolbarComponent] },
      add: { imports: [StubToolbarComponent] }
    });

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    fixture.destroy();

    expect(theming.setFont).not.toHaveBeenCalled();
    expect(addListener).not.toHaveBeenCalledWith('resize', jasmine.any(Function));
    expect(removeListener).not.toHaveBeenCalledWith('resize', jasmine.any(Function));
  });
});
