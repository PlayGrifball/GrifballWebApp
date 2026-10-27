import { DatePipe } from '@angular/common';
import { Component, ChangeDetectionStrategy, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule} from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { AccountService } from '../account.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ThemeComponent } from '../theme/theme.component';
import { ApiClientService } from '../api/apiClient.service';

@Component({
    selector: 'app-toolbar',
    imports: [
        RouterLink,
        DatePipe,
        MatToolbarModule,
        MatButtonModule,
        MatIconModule,
        MatDialogModule,
    ],
    templateUrl: './toolbar.component.html',
    styleUrl: './toolbar.component.css',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ToolbarComponent {
  accountService = inject(AccountService);
  private dialog = inject(MatDialog);
  private api = inject(ApiClientService);

  readonly snav = input.required<MatSidenav>();

  readonly commitHash = toSignal(this.api.commitHash(), { initialValue: null });
  readonly commitDate = toSignal(this.api.commitDate(), { initialValue: null });

  theme(): void {
    this.dialog.open(ThemeComponent, {
      maxWidth: '100%',
      backdropClass: 'no-backdrop',
      maxHeight: '90vh',
      panelClass: 'dialog-no-bg'
    });
  }
}
