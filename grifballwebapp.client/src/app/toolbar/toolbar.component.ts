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
    changeDetection: ChangeDetectionStrategy.OnPush,
    host: { '(document:keydown)': 'onDocumentKeydown($event)' },
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

  /** Whether this is a Mac (or an iPad or iPhone with a keyboard): there the shortcut is Cmd+K, not Ctrl+K. */
  mac = /Mac|iPhone|iPad|iPod/.test(globalThis.navigator?.platform ?? '');

  /** The shortcut as aria-keyshortcuts names it, and as the button's tooltip shows it. */
  get shortcut(): { aria: string; label: string } {
    return this.mac ? { aria: 'Meta+K', label: '⌘K' } : { aria: 'Control+K', label: 'Ctrl+K' };
  }

  /**
   * Ctrl+K (Cmd+K on a Mac) opens the quick search from anywhere, over the browser's own use of it. Not on a Mac's
   * Ctrl+K (a text field's delete to the end of the line), while an IME is composing, when something else has taken
   * the key, or over another dialog. While the quick search is open, it keeps the key from the browser (which would
   * take the focus to its address bar) and puts the focus back in the search.
   */
  onDocumentKeydown(event: KeyboardEvent): void {
    const modifier = this.mac ? event.metaKey && !event.ctrlKey : event.ctrlKey && !event.metaKey;
    if (!modifier || event.altKey || event.shiftKey || event.key?.toLowerCase() !== 'k')
      return;
    if (event.isComposing || event.defaultPrevented)
      return;
    if (this.searchOpen) {
      event.preventDefault();
      document.getElementById('quick-search-input')?.focus();
      return;
    }
    if (document.querySelector('mat-dialog-container'))
      return;
    event.preventDefault();
    void this.search();
  }

  /** Whether the quick search is open (or opening): Ctrl+K again does not open a second one. */
  private searchOpen = false;

  /**
   * Opens the quick search (players, teams, seasons). It and the dialog code load on first use, not with the app. When
   * that load fails (a deploy replaced the chunks, or the network dropped), the search can be opened again.
   */
  async search(): Promise<void> {
    if (this.searchOpen)
      return;
    this.searchOpen = true;
    try {
      const [{ QuickSearchComponent }, { MatDialog }] = await Promise.all([
        import('../quickSearch/quickSearch.component'),
        import('@angular/material/dialog'),
      ]);
      // Near the top, as a command palette is; on a phone it takes the screen's width. Esc, a click beside it and
      // going to a hit (closeOnNavigation) close it.
      const ref = this.injector.get(MatDialog).open(QuickSearchComponent, {
        width: '640px',
        maxWidth: '100vw',
        position: { top: '10vh' },
        panelClass: ['dialog-no-bg', 'quick-search-dialog'],
        ariaLabel: 'Quick search',
        autoFocus: '#quick-search-input',
        restoreFocus: true,
        closeOnNavigation: true,
      });
      ref.afterClosed().subscribe(() => this.searchOpen = false);
    } catch (error) {
      this.searchOpen = false;
      throw error;
    }
  }

  /** Opens the theme editor. It, its colour picker and the dialog code load on first use, not with the app. */
  async theme(): Promise<void> {
    const [{ ThemeComponent }, { MatDialog }] = await Promise.all([
      import('../theme/theme.component'),
      import('@angular/material/dialog'),
    ]);
    // On a phone it takes the whole screen; its header (with Close) stays in view as it scrolls.
    // Esc, a click beside it and going back (closeOnNavigation) close it too.
    this.injector.get(MatDialog).open(ThemeComponent, {
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
  }
}
