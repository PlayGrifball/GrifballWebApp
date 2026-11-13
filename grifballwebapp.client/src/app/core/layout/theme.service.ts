import { effect, Injectable, signal, WritableSignal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {

  constructor() {
    const themeStr = localStorage.getItem("theme");
    if (themeStr !== null) {
      const theme = isTheme(themeStr);
      if (theme !== null) {
        this.currentTheme.set(theme);
      }
    }

    effect(() => {
      const currentTheme = this.currentTheme();
      this.applyTheme(currentTheme);
    })
  }

  applyTheme(theme: themes): void {
    const bodyElement = document.body;

    const previousTheme = this.previousTheme();

    if (previousTheme !== '')
      bodyElement.classList.remove(this.previousTheme());

    if (theme !== '')
      bodyElement.classList.add(theme);
  }

  previousTheme: WritableSignal<themes> = signal('');
  currentTheme: WritableSignal<themes> = signal('');

  buttonClicked() {
    this.previousTheme.set(this.currentTheme());
    this.currentTheme.set(this.next());
    localStorage.setItem('theme', this.currentTheme());
  }

  next(): themes {
    // The default theme toggles to the light one, and the light one back
    return this.currentTheme() === '' ? 'light-theme' : '';
  }
}

const themeNames = ['', 'light-theme'] as const;
//export type themes = '' | 'light-theme';
export type themes = typeof themeNames[number];

function isTheme(maybeTheme: string): themes | null {
  const isTheme = ['', 'light-theme'].includes(maybeTheme);
  if (isTheme) {
    return maybeTheme as themes;
  };
  return null;
}
