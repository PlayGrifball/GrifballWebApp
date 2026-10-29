import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ToolbarComponent } from './toolbar.component';
import { ThemeComponent } from '../theme/theme.component';
import { AccountService } from '../account.service';

describe('ToolbarComponent', () => {
  let component: ToolbarComponent;
  let fixture: ComponentFixture<ToolbarComponent>;
  let account: { isLoggedIn: ReturnType<typeof signal<boolean>>, displayName: ReturnType<typeof signal<string | null>>, personID: ReturnType<typeof signal<number | null>> };
  let mockSidenav: jasmine.SpyObj<MatSidenav>;

  beforeEach(async () => {
    account = { isLoggedIn: signal(false), displayName: signal(null), personID: signal(null) };
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
    expect(el().querySelector('a[aria-label="Profile icon"]')).toBeNull();
  });

  it("shows a signed-in user's name and initial, linked to their profile", () => {
    account.isLoggedIn.set(true);
    account.displayName.set('sarge');
    account.personID.set(5);
    fixture.detectChanges();

    const chip = el().querySelector('a[aria-label="Profile icon"]')!;
    expect(chip.getAttribute('href')).toBe('/profile/5');
    expect(chip.querySelector('.hud-user__avatar')!.textContent!.trim()).toBe('S');
    expect(chip.querySelector('.hud-user__name')!.textContent!.trim()).toBe('sarge');
    expect(el().querySelector('a[aria-label="Login icon"]')).toBeNull();
  });

  it('uses a placeholder initial while the name is unknown', () => {
    account.displayName.set('  ');
    expect(component.initial()).toBe('?');
    account.displayName.set(null);
    expect(component.initial()).toBe('?');
  });

  it('opens the theme editor as a borderless dialog', async () => {
    // The component imports MatDialogModule, so it gets its own MatDialog.
    const open = spyOn(component['dialog'], 'open');
    await component.theme();
    expect(open).toHaveBeenCalledWith(ThemeComponent, {
      maxWidth: '100%',
      backdropClass: 'no-backdrop',
      maxHeight: '90vh',
      panelClass: 'dialog-no-bg'
    });
  });
});
