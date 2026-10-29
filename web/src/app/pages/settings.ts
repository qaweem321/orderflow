import { Component, inject } from '@angular/core';
import { AuthService } from '../core/auth.service';
import { ThemeChoice, ThemeService } from '../core/theme.service';

@Component({
  selector: 'app-settings',
  template: `
    <h1>Settings</h1>
    @if (auth.user(); as user) {
      <section class="card">
        <h2>Account</h2>
        <p>{{ user.displayName }} &middot; {{ user.email }} &middot; {{ user.role }}</p>
      </section>
    }
    <section class="card">
      <h2>Appearance</h2>
      <fieldset class="choices">
        <legend>Theme</legend>
        @for (option of options; track option.value) {
          <label>
            <input type="radio" name="theme" [value]="option.value" [checked]="theme.choice() === option.value" (change)="theme.set(option.value)" />
            {{ option.label }}
          </label>
        }
      </fieldset>
    </section>
  `,
})
export class SettingsPage {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  protected readonly options: { value: ThemeChoice; label: string }[] = [
    { value: 'system', label: 'System (follow your device)' },
    { value: 'light', label: 'Light' },
    { value: 'dark', label: 'Dark' },
  ];
}
