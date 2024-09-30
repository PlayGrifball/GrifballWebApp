import { CommonModule } from '@angular/common';
import { Component, computed, HostBinding, Input, Signal } from '@angular/core';
import { MatIconModule} from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { AccountService } from '../account.service';
import { ThemeService } from '../theme.service';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [
    RouterLink,
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './toolbar.component.html',
  styleUrl: './toolbar.component.css'
})
export class ToolbarComponent {
  @Input({ required: true }) snav!: MatSidenav;

  constructor(public accountService: AccountService, public themeService: ThemeService) {
  }
}
