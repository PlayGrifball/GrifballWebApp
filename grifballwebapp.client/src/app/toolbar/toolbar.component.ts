import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { MatIconModule} from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { AccountService } from '../account.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ThemeComponent } from '../theme/theme.component';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [
    RouterLink,
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
  ],
  templateUrl: './toolbar.component.html',
  styleUrl: './toolbar.component.css'
})
export class ToolbarComponent {
  @Input({ required: true }) snav!: MatSidenav;

  constructor(public accountService: AccountService, private router: Router, private dialog: MatDialog) {
  }

  theme(): void {
    this.dialog.open(ThemeComponent, {
      maxWidth: '100%',
      backdropClass: 'no-backdrop',
      maxHeight: '90vh',
      panelClass: 'dialog-no-bg'
    });
  }
}
