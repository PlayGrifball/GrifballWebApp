import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-season-manager',
  standalone: true,
  imports: [
    CommonModule,
  ],
  templateUrl: './seasonManager.component.html',
  styleUrl: './seasonManager.component.scss'
})
export class SeasonManagerComponent { }
