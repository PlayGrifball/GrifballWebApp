import { Injectable, signal } from '@angular/core';
import { argbFromHex, hexFromArgb, TonalPalette } from '@material/material-color-utilities';

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
    this.setShade('primary', hex);
  }

  setSecondaryShade(hex: string) {
    this.setShade('secondary', hex);
  }

  setTertiaryShade(hex: string) {
    this.setShade('tertiary', hex);
  }

  setNeutralShade(hex: string) {
    this.setShade('neutral', hex);
  }

  setNeutralVariantShade(hex: string) {
    this.setShade('neutral-variant', hex);
  }

  setErrorShade(hex: string) {
    this.setShade('error', hex);
  }

  private setShade(color: string, hex: string) {
    const root = document.querySelector('html');
    if (!root)
    {
      console.log('root missing');
      return;
    }

    root.style.setProperty(`--${color}`, hex);
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
