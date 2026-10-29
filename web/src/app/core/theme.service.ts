import { Injectable, signal } from '@angular/core';

export type ThemeChoice = 'system' | 'light' | 'dark';

/**
 * public/theme.js applies the saved theme before first paint (no flash) and follows OS and other-tab
 * changes; this service only lets the Settings page read and change the choice.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly choice = signal<ThemeChoice>(read());

  set(choice: ThemeChoice): void {
    this.choice.set(choice);
    try {
      if (choice === 'system') {
        localStorage.removeItem('theme');
      } else {
        localStorage.setItem('theme', choice);
      }
    } catch {
      // Ignore: the theme still applies for this page.
    }
    (window as unknown as { applyTheme?: () => void }).applyTheme?.();
  }
}

function read(): ThemeChoice {
  try {
    const saved = localStorage.getItem('theme');
    return saved === 'light' || saved === 'dark' ? saved : 'system';
  } catch {
    return 'system';
  }
}
