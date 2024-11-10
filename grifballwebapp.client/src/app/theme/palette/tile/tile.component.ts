import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, Signal } from '@angular/core';
import { palette } from '../../paletteTypes';
import { ThemingService } from '../../../theming.service';

@Component({
  selector: 'app-tile',
  standalone: true,
  imports: [
    CommonModule,
  ],
  templateUrl: './tile.component.html',
  styleUrl: './tile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TileComponent {
  paletteName = input.required<palette>();
  i = input.required<number | null>();

  constructor(private themingService: ThemingService) {}

  textColor = computed(() => {
    const i = this.i();
    if (i == null || i < 80)
      return `text-white`;
    else
      return `text-black`;
  });

  bg = computed(() => {
    const i = this.i();
    if (i == null)
      return `bg-${this.paletteName()}`;
    else
      return `bg-${this.paletteName()}-${i}`;
  });

  hex: Signal<string | undefined> = computed(() => {
    const x = this.themingService.shadeApplied();
    const i = this.i();
    if (i == null)
      return document.querySelector('html')?.style.getPropertyValue(`--${this.paletteName()}`);
    else
      return document.querySelector('html')?.style.getPropertyValue(`--${this.paletteName()}-${i}`);
  });
}
