import { DestroyRef, Injectable, PLATFORM_ID, effect, inject, signal, untracked } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { AccountService } from '../auth/account.service';
import { AppLog } from '../telemetry/appLog';
import { MyThemeService } from '@api/services/my-theme.service';
import { ThemeSettings, ThemingService } from './theming.service';

/** Where this device keeps the theme, so it shows when nobody is signed in and before the account's has loaded. */
export const THEME_STORAGE_KEY = 'themeSettings';

/** How long a change waits for another before it is saved to the account (the colour picker changes it many times a second). */
export const THEME_SAVE_DELAY_MS = 800;

const HEX = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i;
const FONTS = ['Rajdhani', 'Roboto', 'sans-serif'];

/** The settings if `value` is a theme the picker can make (stored text is anyone's to edit), else null. */
export function parseThemeSettings(value: unknown): ThemeSettings | null {
  if (typeof value !== 'object' || value === null)
    return null;
  const v = value as Record<string, unknown>;
  const colours = [v['primary'], v['secondary'], v['tertiary'], v['neutral'], v['neutralVariant'], v['error']];
  if (typeof v['era'] !== 'string' || !/^[a-z0-9-]{0,32}$/.test(v['era'])
    || typeof v['font'] !== 'string' || !FONTS.includes(v['font'])
    || !colours.every(c => typeof c === 'string' && HEX.test(c)))
    return null;
  return {
    era: v['era'], font: v['font'],
    primary: v['primary'] as string, secondary: v['secondary'] as string, tertiary: v['tertiary'] as string,
    neutral: v['neutral'] as string, neutralVariant: v['neutralVariant'] as string, error: v['error'] as string,
  };
}

/**
 * Keeps the player's theme. Every change is saved on this device (localStorage, so it applies when signed out
 * and from the first paint) and, when signed in, to their account, so their other devices show it too. On
 * signing in, the account's theme wins; an account with none takes this device's (when it has one).
 */
@Injectable({ providedIn: 'root' })
export class ThemePersistence {
  private readonly theming = inject(ThemingService);
  private readonly account = inject(AccountService);
  private readonly api = inject(MyThemeService);
  private readonly log = inject(AppLog);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  /** The settings (as JSON) last put on the site from storage, the account or a change, so only a new choice is saved. */
  private current = '';
  private timer: ReturnType<typeof setTimeout> | null = null;

  /** Counts the times a saved theme from the account replaced the one shown: the editor starts over from it. */
  readonly restored = signal(0);

  constructor() {
    if (!this.browser)
      return;

    const stored = this.readStored();
    if (stored)
      this.theming.applySettings(stored);
    this.current = JSON.stringify(this.theming.settings());

    effect(() => {
      const settings = this.theming.settings();
      untracked(() => this.changed(settings));
    });
    effect(() => {
      if (this.account.personID() !== null)
        untracked(() => this.loadFromAccount());
    });
    inject(DestroyRef).onDestroy(() => this.cancelSave());
  }

  private changed(settings: ThemeSettings): void {
    const json = JSON.stringify(settings);
    if (json === this.current)
      return;
    this.current = json;
    this.writeStored(json);
    if (this.account.isLoggedIn()) {
      this.cancelSave();
      this.timer = setTimeout(() => this.saveToAccount(settings), THEME_SAVE_DELAY_MS);
    }
  }

  private loadFromAccount(): void {
    this.api.getMyTheme().subscribe({
      next: saved => {
        const settings = parseThemeSettings(saved);
        if (settings) {
          this.cancelSave();
          this.current = JSON.stringify(settings);
          this.writeStored(this.current);
          this.theming.applySettings(settings);
          this.restored.update(n => n + 1);
        } else if (this.readStored()) {
          this.saveToAccount(this.theming.settings());
        }
      },
      error: () => this.log.warn('Theme: could not load the saved theme'),
    });
  }

  private saveToAccount(settings: ThemeSettings): void {
    this.timer = null;
    this.api.setMyTheme({ body: settings }).subscribe({
      error: () => this.log.warn('Theme: could not save the theme to the account'),
    });
  }

  private cancelSave(): void {
    if (this.timer !== null)
      clearTimeout(this.timer);
    this.timer = null;
  }

  private readStored(): ThemeSettings | null {
    try {
      const text = localStorage.getItem(THEME_STORAGE_KEY);
      return text ? parseThemeSettings(JSON.parse(text)) : null;
    } catch {
      return null;
    }
  }

  private writeStored(json: string): void {
    try {
      localStorage.setItem(THEME_STORAGE_KEY, json);
    } catch {
      // Storage blocked or full: the theme still applies for this visit
    }
  }
}
