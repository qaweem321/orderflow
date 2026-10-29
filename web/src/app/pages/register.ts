import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { errorMessage } from '../core/format';
import { PasswordInput } from '../shared/password-input';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink, PasswordInput],
  template: `
    <section class="auth-card card">
      <h1>Create an account</h1>
      <form (ngSubmit)="submit()">
        <label for="name">Your name</label>
        <input id="name" name="name" autocomplete="name" required minlength="2" maxlength="80" [(ngModel)]="displayName" />
        <label for="email">Email</label>
        <input id="email" name="email" type="email" autocomplete="email" required [(ngModel)]="email" />
        <label for="password">Password <span class="muted small">(at least 10 characters)</span></label>
        <app-password-input inputId="password" autocomplete="new-password" [minLength]="10" [(value)]="password" />
        @if (error()) {
          <p class="alert error" role="alert">{{ error() }}</p>
        }
        <button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Creating account...' : 'Create account' }}</button>
      </form>
      <p>Already have an account? <a routerLink="/login" [queryParams]="{ next: next() }" class="text-link">Sign in</a>.</p>
    </section>
  `,
})
export class RegisterPage {
  readonly next = input<string | null>(null);
  private readonly auth = inject(AuthService);
  protected displayName = '';
  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.register(this.email, this.displayName, this.password, this.next());
    } catch (e) {
      this.error.set(errorMessage(e));
      this.busy.set(false);
    }
  }
}
