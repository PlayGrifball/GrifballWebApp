import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, computed, inject, input, signal, viewChild, viewChildren } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { AccountService } from '../account.service';

@Component({
    selector: 'app-toolbar',
    imports: [
        RouterLink,
        MatButtonModule,
        MatIconModule,
    ],
    templateUrl: './toolbar.component.html',
    styleUrl: './toolbar.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ToolbarComponent {
  accountService = inject(AccountService);
  private injector = inject(Injector);

  readonly snav = input.required<MatSidenav>();
  /** The menu button opens the sidenav; it is only needed when some links do not fit in the bar. */
  readonly showMenu = input(true);

  /** The letter on the user's square avatar. */
  readonly initial = computed(() => this.accountService.displayName()?.trim().charAt(0).toUpperCase() || '?');

  /** The signed-in user's menu (Profile, Log out), opened from their chip. */
  readonly menuOpen = signal(false);
  private readonly chip = viewChild<ElementRef<HTMLElement>>('chip');
  private readonly menuItems = viewChildren<ElementRef<HTMLElement>>('menuItem');

  /** Opens the menu with the focus on its first item, as a menu button does, or closes it. */
  toggleMenu(): void {
    if (this.menuOpen()) {
      this.closeMenu();
      return;
    }
    this.menuOpen.set(true);
    afterNextRender(() => this.menuItems()[0]?.nativeElement.focus(), { injector: this.injector });
  }

  /** Closes the menu; `refocus` puts the focus back on the chip (after Esc). */
  closeMenu(refocus = false): void {
    this.menuOpen.set(false);
    if (refocus)
      this.chip()?.nativeElement.focus();
  }

  /** Esc closes the menu; the arrow keys, Home and End move between its items. */
  onMenuKeydown(event: KeyboardEvent): void {
    if (!this.menuOpen())
      return;
    const items = this.menuItems().map(i => i.nativeElement);
    const at = items.indexOf(document.activeElement as HTMLElement);
    const move = (to: number) => { event.preventDefault(); items[(to + items.length) % items.length]?.focus(); };
    switch (event.key) {
      case 'Escape': event.preventDefault(); this.closeMenu(true); break;
      case 'ArrowDown': move(at + 1); break;
      case 'ArrowUp': move(at < 0 ? items.length - 1 : at - 1); break;
      case 'Home': move(0); break;
      case 'End': move(items.length - 1); break;
    }
  }

  /** The menu closes when the focus leaves it and the chip, as on a click elsewhere or Tab. */
  onMenuFocusOut(event: FocusEvent): void {
    const container = event.currentTarget as HTMLElement;
    const next = event.relatedTarget as Node | null;
    if (!next || !container.contains(next))
      this.closeMenu();
  }

  /** Logs out from the menu (the account service then goes home, signed out). */
  logout(): void {
    this.closeMenu();
    this.accountService.logout();
  }

  /** Opens the theme editor. It, its colour picker and the dialog code load on first use, not with the app. */
  async theme(): Promise<void> {
    const [{ ThemeComponent }, { MatDialog }] = await Promise.all([
      import('../theme/theme.component'),
      import('@angular/material/dialog'),
    ]);
    this.injector.get(MatDialog).open(ThemeComponent, {
      maxWidth: '100%',
      backdropClass: 'no-backdrop',
      maxHeight: '90vh',
      panelClass: 'dialog-no-bg'
    });
  }
}
