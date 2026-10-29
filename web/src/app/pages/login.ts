import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { errorMessage } from '../core/format';
import { PasswordInput } from '../shared/password-input';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, PasswordInput],
  template: `
    <section class="auth-card card">
      <h1>Sign in</h1>
      <form (ngSubmit)="submit()">
        <label for="email">Email</label>
        <input id="email" name="email" type="email" autocomplete="email" required [(ngModel)]="email" />
        <label for="password">Password</label>
        <app-password-input inputId="password" [(value)]="password" />
        @if (error()) {
          <p class="alert error" role="alert">{{ error() }}</p>
        }
        <button class="button" type="submit" [disabled]="busy()">{{ busy() ? 'Signing in...' : 'Sign in' }}</button>
      </form>
      <p>New here? <a routerLink="/register" [queryParams]="{ next: next() }" class="text-link">Create an account</a>.</p>
    </section>
  `,
})
export class LoginPage {
  readonly next = input<string | null>(null);
  private readonly auth = inject(AuthService);
  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.login(this.email, this.password, this.next());
    } catch (e) {
      this.error.set(errorMessage(e));
      this.busy.set(false);
    }
  }
}
