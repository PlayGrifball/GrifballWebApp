import { Injectable, signal, WritableSignal } from '@angular/core';

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
  }

  currentTheme: WritableSignal<themes> = signal('');

  buttonClicked() {
    this.currentTheme.set(this.next());
    localStorage.setItem('theme', this.currentTheme());
  }

  next(): themes {
    switch(this.currentTheme()) {
      case '': return 'light-theme';
      case 'light-theme': return '';
      default: '';
    }
    return '';
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