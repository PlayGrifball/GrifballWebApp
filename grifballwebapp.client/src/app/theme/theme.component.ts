import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PaletteComponent } from './palette/palette.component';

@Component({
  selector: 'app-theme',
  standalone: true,
  imports: [
    CommonModule,
    PaletteComponent,
  ],
  templateUrl: './theme.component.html',
  styleUrl: './theme.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeComponent {
}
