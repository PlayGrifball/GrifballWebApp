import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ToolbarComponent } from './toolbar.component';
import { ThemeComponent } from '../theme/theme.component';
import { AccountService } from '../account.service';
import { MatDialog } from '@angular/material/dialog';

describe('ToolbarComponent', () => {
  let component: ToolbarComponent;
  let fixture: ComponentFixture<ToolbarComponent>;
  let account: { isLoggedIn: ReturnType<typeof signal<boolean>>, displayName: ReturnType<typeof signal<string | null>>, personID: ReturnType<typeof signal<number | null>>, logout: jasmine.Spy };
  let mockSidenav: jasmine.SpyObj<MatSidenav>;

  beforeEach(async () => {
    account = { isLoggedIn: signal(false), displayName: signal(null), personID: signal(null), logout: jasmine.createSpy('logout') };
    mockSidenav = jasmine.createSpyObj('MatSidenav', ['toggle']);

    await TestBed.configureTestingModule({
      imports: [ToolbarComponent, NoopAnimationsModule],
      providers: [
        provideRouter([]),
        { provide: AccountService, useValue: account },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ToolbarComponent);
    fixture.componentRef.setInput('snav', mockSidenav);
    component = fixture.componentInstance;
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have account service injected', () => {
    expect(component.accountService).toBe(account as unknown as AccountService);
  });

  it('toggles the sidenav from the menu button', () => {
    fixture.detectChanges();
    el().querySelector<HTMLButtonElement>('button[aria-label="menu icon"]')!.click();

    expect(mockSidenav.toggle).toHaveBeenCalled();
  });

  it('leaves the menu button out when every link fits in the bar', () => {
    fixture.componentRef.setInput('showMenu', false);
    fixture.detectChanges();

    expect(el().querySelector('button[aria-label="menu icon"]')).toBeNull();
  });

  it('links the wordmark home', () => {
    fixture.detectChanges();
    expect(el().querySelector('a.hud-brand')!.getAttribute('href')).toBe('/');
  });

  it('offers a login link to visitors', () => {
    fixture.detectChanges();
    const login = el().querySelector('a[aria-label="Login icon"]')!;
    expect(login.getAttribute('href')).toBe('/login');
    expect(el().querySelector('.hud-user')).toBeNull();
  });

  describe('signed in', () => {
    beforeEach(() => {
      account.isLoggedIn.set(true);
      account.displayName.set('sarge');
      account.personID.set(5);
      fixture.detectChanges();
    });

    const chip = () => el().querySelector<HTMLButtonElement>('button.hud-user')!;
    const items = () => Array.from(el().querySelectorAll<HTMLElement>('[role="menuitem"]'));
    const key = (key: string) => {
      const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
      (document.activeElement ?? chip()).dispatchEvent(event);
      fixture.detectChanges();
      return event;
    };
    const open = async () => {
      chip().click();
      fixture.detectChanges();
      await fixture.whenStable();
    };

    it("shows the user's name and initial on a chip that is a menu button, closed", () => {
      expect(chip().querySelector('.hud-user__avatar')!.textContent!.trim()).toBe('S');
      expect(chip().querySelector('.hud-user__name')!.textContent!.trim()).toBe('sarge');
      expect(chip().getAttribute('aria-haspopup')).toBe('menu');
      expect(chip().getAttribute('aria-expanded')).toBe('false');
      expect(items()).toEqual([]);
      expect(el().querySelector('a[aria-label="Login icon"]')).toBeNull();
    });

    it('opens a menu with Profile (their own) and Log out, the focus on the first', async () => {
      await open();
      expect(chip().getAttribute('aria-expanded')).toBe('true');
      expect(el().querySelector('[role="menu"]')!.id).toBe(chip().getAttribute('aria-controls')!);
      expect(items().map(i => i.textContent!.trim())).toEqual(['Profile', 'Log out']);
      expect(items()[0].getAttribute('href')).toBe('/profile/5');
      expect(document.activeElement).toBe(items()[0]);
    });

    it('moves with the arrow keys, Home and End, round the ends', async () => {
      await open();
      key('ArrowDown');
      expect(document.activeElement).toBe(items()[1]);
      key('ArrowDown');
      expect(document.activeElement).toBe(items()[0]);
      key('ArrowUp');
      expect(document.activeElement).toBe(items()[1]);
      key('Home');
      expect(document.activeElement).toBe(items()[0]);
      key('End');
      expect(document.activeElement).toBe(items()[1]);
      expect(key('Tab').defaultPrevented).toBeFalse();
    });

    it('closes on Esc, with the focus back on the chip', async () => {
      await open();
      expect(key('Escape').defaultPrevented).toBeTrue();
      expect(items()).toEqual([]);
      expect(document.activeElement).toBe(chip());
    });

    it('ignores keys while closed', () => {
      expect(key('Escape').defaultPrevented).toBeFalse();
      expect(key('ArrowDown').defaultPrevented).toBeFalse();
    });

    it('closes when the chip is pressed again', async () => {
      await open();
      chip().click();
      fixture.detectChanges();
      expect(items()).toEqual([]);
    });

    it('closes when the focus leaves it, not when it moves inside it', async () => {
      await open();
      const container = el().querySelector('.hud-user-menu')!;
      container.dispatchEvent(new FocusEvent('focusout', { relatedTarget: items()[1] }));
      fixture.detectChanges();
      expect(items().length).toBe(2);
      container.dispatchEvent(new FocusEvent('focusout', { relatedTarget: null }));
      fixture.detectChanges();
      expect(items()).toEqual([]);
    });

    it('closes when Profile is chosen', async () => {
      await open();
      items()[0].click();
      fixture.detectChanges();
      expect(items()).toEqual([]);
    });

    it('logs out from the menu, and closes it', async () => {
      await open();
      items()[1].click();
      fixture.detectChanges();
      expect(account.logout).toHaveBeenCalled();
      expect(items()).toEqual([]);
    });
  });

  it('uses a placeholder initial while the name is unknown', () => {
    account.displayName.set('  ');
    expect(component.initial()).toBe('?');
    account.displayName.set(null);
    expect(component.initial()).toBe('?');
  });

  it('opens the theme editor as a borderless dialog, full screen on a phone and closable every way', async () => {
    // The dialog code is loaded on the first click; the app-wide MatDialog opens it.
    const open = spyOn(TestBed.inject(MatDialog), 'open');
    await component.theme();
    expect(open).toHaveBeenCalledWith(ThemeComponent, {
      width: '720px',
      maxWidth: '100vw',
      maxHeight: '100dvh',
      backdropClass: 'no-backdrop',
      panelClass: ['dialog-no-bg', 'theme-dialog'],
      ariaLabelledBy: 'theme-title',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      closeOnNavigation: true,
    });
  });
});
