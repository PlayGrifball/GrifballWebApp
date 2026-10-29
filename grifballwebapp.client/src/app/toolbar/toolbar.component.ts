import { ChangeDetectionStrategy, Component, Injector, computed, inject, input } from '@angular/core';
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
