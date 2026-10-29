import { Component, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

/** A password field with an eye button that shows or hides what was typed. */
@Component({
  selector: 'app-password-input',
  imports: [FormsModule],
  template: `
    <div class="password">
      <input
        [id]="inputId()"
        [type]="visible() ? 'text' : 'password'"
        [name]="inputId()"
        [autocomplete]="autocomplete()"
        [minlength]="minLength()"
        required
        [(ngModel)]="value"
      />
      <button
        type="button"
        class="icon-button"
        [attr.aria-label]="visible() ? 'Hide password' : 'Show password'"
        [attr.aria-pressed]="visible()"
        (click)="visible.set(!visible())"
      >
        @if (visible()) {
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 3l18 18M10.6 10.6a2 2 0 002.8 2.8M9.9 5.1A9.8 9.8 0 0112 5c5 0 9 4.5 10 7-.4 1-1.2 2.3-2.4 3.5M6.6 6.6C4.4 8 2.8 10.2 2 12c1 2.5 5 7 10 7 1.6 0 3-.4 4.3-1" /></svg>
        } @else {
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 12c1-2.5 5-7 10-7s9 4.5 10 7c-1 2.5-5 7-10 7S3 14.5 2 12z" /><circle cx="12" cy="12" r="3" /></svg>
        }
      </button>
    </div>
  `,
})
export class PasswordInput {
  readonly inputId = input.required<string>();
  readonly autocomplete = input('current-password');
  readonly minLength = input(1);
  readonly value = model('');
  protected readonly visible = signal(false);
}
