import { ChangeDetectionStrategy, Component, computed, input, Signal, inject } from '@angular/core';
import { palette } from '../../paletteTypes';
import { ThemingService } from '../../../theming.service';

@Component({
    selector: 'app-tile',
    imports: [],
    templateUrl: './tile.component.html',
    styleUrl: './tile.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TileComponent {
  private themingService = inject(ThemingService);

  paletteName = input.required<palette>();
  i = input.required<number | null>();

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
