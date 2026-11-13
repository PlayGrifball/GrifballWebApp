import { Injectable, signal } from '@angular/core';

type Theme = {
  name: string;
  primary: string;
  secondary: string;
  tertiary: string;
  neutral: string;
  neutralVariant: string;
  error: string;
  font?: string;
};

/** The default theme: the seed colours of the Spartan HUD design (see styles/_hud-tokens.scss). */
export const SPARTAN_HUD = {
  name: 'Spartan HUD',
  primary: '#7CCBFF',
  secondary: '#2A5373',
  tertiary: '#FFD25E',
  neutral: '#5F7F96',
  neutralVariant: '#4F7391',
  error: '#E5484D',
  font: 'Rajdhani',
} as const satisfies Theme;

export type ShadeApplied = {
  name: string,
  hex: string,
}

@Injectable({
  providedIn: 'root',
})
export class ThemingService {
  // Spartan HUD is the default theme. The picker edits these seed colours: the accent
  // (primary), gold (tertiary) and team red (error) of the HUD follow them, see styles/_hud-tokens.scss.
  font = signal<string>(SPARTAN_HUD.font);
  primary = signal<string>(SPARTAN_HUD.primary);
  secondary = signal<string>(SPARTAN_HUD.secondary);
  tertiary = signal<string>(SPARTAN_HUD.tertiary);
  neutral = signal<string>(SPARTAN_HUD.neutral);
  neutralVariant = signal<string>(SPARTAN_HUD.neutralVariant);
  error = signal<string>(SPARTAN_HUD.error);

  shadeApplied = signal<ShadeApplied>({
    name: '',
    hex: '',
  });

  constructor() {
  }

  definedThemes: Theme[] = [
    SPARTAN_HUD,
    {
      name: 'Classic',
      primary: '#FFDE03',
      secondary: '#806f00',
      tertiary: '#A288A6',
      neutral: '#919093',
      neutralVariant: '#8E9098',
      error: '#FF5449',
    },
  ];

  /** Puts the picker back to the Spartan HUD colours and font. */
  reset(): void {
    this.applyTheme(SPARTAN_HUD);
  }

  applyTheme(theme: Theme) {
    const { primary, secondary, tertiary, neutral, neutralVariant, error, font } = theme;
    if (font)
      this.font.set(font);
    this.primary.set(primary);
    this.secondary.set(secondary);
    this.tertiary.set(tertiary);
    this.neutral.set(neutral);
    this.neutralVariant.set(neutralVariant);
    this.error.set(error);
  }

  setFont(font: string) {
    const root = document.querySelector('html');
    if (root == null) {
      return;
    }
    loadFont(font);
    root?.style.setProperty(`--sys-body-small-font`, font);
    root?.style.setProperty(`--sys-body-medium-font`, font);
    root?.style.setProperty(`--sys-body-large-font`, font);
    root?.style.setProperty(`--sys-display-large-font`, font);
    root?.style.setProperty(`--sys-display-medium-font`, font);
    root?.style.setProperty(`--sys-display-small-font`, font);
    root?.style.setProperty(`--sys-headline-large-font`, font);
    root?.style.setProperty(`--sys-headline-medium-font`, font);
    root?.style.setProperty(`--sys-headline-small-font`, font);
    root?.style.setProperty(`--sys-label-large-font`, font);
    root?.style.setProperty(`--sys-label-medium-font`, font);
    root?.style.setProperty(`--sys-label-small-font`, font);
    root?.style.setProperty(`--sys-title-large-font`, font);
    root?.style.setProperty(`--sys-title-medium-font`, font);
    root?.style.setProperty(`--sys-title-small-font`, font);
  }

  setPrimaryShade(hex: string) {
    return this.setShade('primary', hex);
  }

  setSecondaryShade(hex: string) {
    return this.setShade('secondary', hex);
  }

  setTertiaryShade(hex: string) {
    return this.setShade('tertiary', hex);
  }

  setNeutralShade(hex: string) {
    return this.setShade('neutral', hex);
  }

  setNeutralVariantShade(hex: string) {
    return this.setShade('neutral-variant', hex);
  }

  setErrorShade(hex: string) {
    return this.setShade('error', hex);
  }

  /**
   * Sets a seed colour (which the Spartan HUD accent, gold and red follow at once), then its tonal
   * palette (--<color>-0 to -100, for the picker's tiles and Tailwind's palette classes). The colour
   * utilities that make the palette are loaded on first use, so they are not in the initial bundle.
   */
  private async setShade(color: string, hex: string): Promise<void> {
    const root = document.querySelector('html');
    if (!root)
    {
      console.log('root missing');
      return;
    }

    root.style.setProperty(`--${color}`, hex);
    const { argbFromHex, hexFromArgb, TonalPalette } = await import('@material/material-color-utilities');
    const palette = TonalPalette.fromInt(argbFromHex(hex));

    let increment = 10;
    if (color == 'neutral' || color == 'primary')
      increment = 1;

    for (let i = 0; i <= 100; i += increment) {
      const shade = hexFromArgb(palette.tone(i));
      root.style.setProperty(`--${color}-${i}`, shade);
      //console.log(color + ' ' + i + ' ' + shade.toUpperCase());
    }
    this.shadeApplied.set({
      name: color,
      hex: hex,
    });
  }
}

/** Fonts the picker offers that are not self-hosted: their Google Fonts stylesheet, loaded when picked. */
const ON_DEMAND_FONTS: Record<string, string> = {
  Roboto: 'https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap',
};

/** Adds the stylesheet of an on-demand font to the page, once. */
export function loadFont(font: string): void {
  const href = ON_DEMAND_FONTS[font];
  if (!href || document.head.querySelector(`link[href="${href}"]`))
    return;
  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = href;
  document.head.appendChild(link);
}
