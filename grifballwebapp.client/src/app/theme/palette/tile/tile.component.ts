import { ChangeDetectionStrategy, Component, computed, DOCUMENT, input, Signal, inject } from '@angular/core';
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
  // The page's (the server's, rendering it: no colours set there yet)
  private document = inject(DOCUMENT);

  paletteName = input.required<palette>();
  i = input.required<number | null>();

  /** Black or white, whichever reads better on the tile's colour (by its luminance). */
  textColor = computed(() => readableOn(this.hex()));

  bg = computed(() => {
    const i = this.i();
    if (i == null)
      return `bg-${this.paletteName()}`;
    else
      return `bg-${this.paletteName()}-${i}`;
  });

  hex: Signal<string | undefined> = computed(() => {
    this.themingService.shadeApplied(); // read so the colour updates when the shade does
    const i = this.i();
    if (i == null)
      return this.document.documentElement.style.getPropertyValue(`--${this.paletteName()}`);
    else
      return this.document.documentElement.style.getPropertyValue(`--${this.paletteName()}-${i}`);
  });
}

/**
 * The text colour that reads best on a #rrggbb background: black on light colours, white on dark
 * ones (WCAG relative luminance; the two give equal contrast at 0.179). White when it is unknown.
 */
export function readableOn(hex: string | undefined): '#000000' | '#ffffff' {
  const m = /^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec((hex ?? '').trim());
  if (!m)
    return '#ffffff';
  const [r, g, b] = m.slice(1).map(c => {
    const v = Number.parseInt(c, 16) / 255;
    return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b > 0.179 ? '#000000' : '#ffffff';
}
