import { CommonModule } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { MatIconModule} from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { AccountService } from '../account.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ThemeComponent } from '../theme/theme.component';
import { DateTime } from 'luxon';
import { ApiClientService } from '../api/apiClient.service';

@Component({
    selector: 'app-toolbar',
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
export class ToolbarComponent implements OnInit {
  @Input({ required: true }) snav!: MatSidenav;

  commitHash: string | null = null;
  commitDate: DateTime | null = null;

  constructor(public accountService: AccountService,
    private router: Router,
    private dialog: MatDialog,
    private api: ApiClientService) {
  }

  ngOnInit(): void {
    this.api.commitHash()
      .subscribe({
          next: (x) => this.commitHash = x,
        });

    this.api.commitDate()
      .subscribe({
          next: (x) => this.commitDate = x,
        });
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
