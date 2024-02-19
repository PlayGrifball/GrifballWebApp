import { Component, ViewChild } from '@angular/core';
import { ToolbarComponent } from './toolbar/toolbar.component';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterLink,
    MatListModule,
    MatSidenavModule,
    RouterOutlet,
    ToolbarComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  @ViewChild('snav') snav!: MatSidenav;

  fillerNav = Array.from({ length: 50 }, (_, i) => `Nav Item ${i + 1}`);
}
