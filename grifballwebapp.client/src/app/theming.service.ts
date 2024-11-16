import { Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { argbFromHex, hexFromArgb, TonalPalette } from '@material/material-color-utilities';
import { Subject } from 'rxjs';

type Theme = {
  name: string;
  primary: string;
  secondary: string;
  tertiary: string;
  neutral: string;
  neutralVariant: string;
  error: string;
};

export type ShadeApplied = {
  name: string,
  hex: string,
}

@Injectable({
  providedIn: 'root',
})
export class ThemingService {
  font = signal('Roboto');
  primary = signal('#f35700');
  secondary = signal('#8e0003');
  tertiary = signal('#000582');
  neutral = signal('#919093');
  neutralVariant = signal('#8E9098');
  error = signal('#FF5449');

  shadeApplied = signal<ShadeApplied>({
    name: '',
    hex: '',
  });

  constructor() {
  }

  definedThemes: Theme[] = [
    {
      name: 'Default',
      primary: '#FFDE03',
      secondary: '#806f00',
      tertiary: '#A288A6',
      neutral: '#919093',
      neutralVariant: '#8E9098',
      error: '#FF5449',
    },
  ];

  applyTheme(theme: Theme) {
    const { primary, secondary, tertiary, neutral, neutralVariant, error } = theme;
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
