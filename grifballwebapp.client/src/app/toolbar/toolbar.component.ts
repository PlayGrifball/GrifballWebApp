import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { AccountService } from '../account.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ThemeComponent } from '../theme/theme.component';

@Component({
    selector: 'app-toolbar',
    imports: [
        RouterLink,
        MatButtonModule,
        MatIconModule,
        MatDialogModule,
    ],
    templateUrl: './toolbar.component.html',
    styleUrl: './toolbar.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ToolbarComponent {
  accountService = inject(AccountService);
  private dialog = inject(MatDialog);

  readonly snav = input.required<MatSidenav>();
  /** The menu button opens the sidenav; it is only needed when some links do not fit in the bar. */
  readonly showMenu = input(true);

  /** The letter on the user's square avatar. */
  readonly initial = computed(() => this.accountService.displayName()?.trim().charAt(0).toUpperCase() || '?');

  theme(): void {
    this.dialog.open(ThemeComponent, {
      maxWidth: '100%',
      backdropClass: 'no-backdrop',
      maxHeight: '90vh',
      panelClass: 'dialog-no-bg'
    });
  }
}
