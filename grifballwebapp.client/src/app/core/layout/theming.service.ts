import { Injectable, signal, inject } from '@angular/core';
import { AppLog } from '../telemetry/appLog';

type Theme = {
  name: string;
  era?: string;
  primary: string;
  secondary: string;
  tertiary: string;
  neutral: string;
  neutralVariant: string;
  error: string;
  font?: string;
};

/** The default theme: the seed colours of the Archive Vault design (see styles/_hud-tokens.scss). */
export const ARCHIVE_VAULT = {
  name: 'Archive Vault',
  era: '',
  primary: '#E8922D',
  secondary: '#2C3344',
  tertiary: '#E6C15A',
  neutral: '#6B768F',
  neutralVariant: '#5C667D',
  error: '#E5484D',
  font: 'Rajdhani',
} as const satisfies Theme;

/** Legacy Spartan HUD preset (cyan-on-navy). */
export const SPARTAN_HUD = {
  name: 'Spartan HUD',
  era: 'spartan-hud',
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
  private readonly log = inject(AppLog);
  // Archive Vault is the default theme. The picker edits these seed colours: the accent
  // (primary), gold (tertiary) and team red (error) follow them, see styles/_hud-tokens.scss.
  era = signal<string>(ARCHIVE_VAULT.era);
  font = signal<string>(ARCHIVE_VAULT.font);
  primary = signal<string>(ARCHIVE_VAULT.primary);
  secondary = signal<string>(ARCHIVE_VAULT.secondary);
  tertiary = signal<string>(ARCHIVE_VAULT.tertiary);
  neutral = signal<string>(ARCHIVE_VAULT.neutral);
  neutralVariant = signal<string>(ARCHIVE_VAULT.neutralVariant);
  error = signal<string>(ARCHIVE_VAULT.error);

  shadeApplied = signal<ShadeApplied>({
    name: '',
    hex: '',
  });

  constructor() {
  }

  definedThemes: Theme[] = [
    ARCHIVE_VAULT,
    {
      name: '2008 Halo 3',
      era: '2008',
      primary: '#E5484D',
      secondary: '#4A282D',
      tertiary: '#FFD25E',
      neutral: '#8D5860',
      neutralVariant: '#6E454B',
      error: '#E5484D',
      font: 'Rajdhani',
    },
    {
      name: '2011 Reach Tactical',
      era: '2011',
      primary: '#EAB308',
      secondary: '#2E3D31',
      tertiary: '#FDE047',
      neutral: '#64826A',
      neutralVariant: '#4C6451',
      error: '#E5484D',
      font: 'Rajdhani',
    },
    {
      name: '2014 GrifballHub',
      era: '2014',
      primary: '#F97316',
      secondary: '#303B4F',
      tertiary: '#FACC15',
      neutral: '#6B7C9C',
      neutralVariant: '#53627D',
      error: '#E5484D',
      font: 'Rajdhani',
    },
    SPARTAN_HUD,
    {
      name: 'Classic',
      era: '',
      primary: '#FFDE03',
      secondary: '#806f00',
      tertiary: '#A288A6',
      neutral: '#919093',
      neutralVariant: '#8E9098',
      error: '#FF5449',
    },
  ];

  /** Puts the picker back to the Archive Vault colours and font. */
  reset(): void {
    this.applyTheme(ARCHIVE_VAULT);
  }

  applyTheme(theme: Theme) {
    const { primary, secondary, tertiary, neutral, neutralVariant, error, font, era } = theme;
    this.era.set(era ?? '');
    if (font)
      this.font.set(font);
    this.primary.set(primary);
    this.secondary.set(secondary);
    this.tertiary.set(tertiary);
    this.neutral.set(neutral);
    this.neutralVariant.set(neutralVariant);
    this.error.set(error);
  }

  setEra(era: string) {
    const root = document.querySelector('html');
    if (root == null) {
      return;
    }
    if (era)
      root.dataset['themeEra'] = era;
    else
      delete root.dataset['themeEra'];
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
      this.log.warn('Theme: no <html> element to set the shade on');
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
