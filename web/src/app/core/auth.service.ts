import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { safeNextPath } from './format';
import { Session } from './models';

const STORAGE_KEY = 'orderflow.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<Session | null>(readSession());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.user()?.role === 'admin');

  async login(email: string, password: string, next: string | null): Promise<void> {
    this.store(await firstValueFrom(this.http.post<Session>('/api/auth/login', { email, password })), next);
  }

  async register(email: string, displayName: string, password: string, next: string | null): Promise<void> {
    this.store(await firstValueFrom(this.http.post<Session>('/api/auth/register', { email, displayName, password })), next);
  }

  /** Full page load (replace, not push), so Back can't show pages from the signed-in session. */
  logout(next?: string): void {
    localStorage.removeItem(STORAGE_KEY);
    location.replace(next ? `/login?next=${encodeURIComponent(next)}` : '/login');
  }

  private store(session: Session, next: string | null): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    location.replace(safeNextPath(next));
  }
}

function readSession(): Session | null {
  try {
    const session = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as Session | null;
    return session && new Date(session.expiresAt) > new Date() ? session : null;
  } catch {
    return null;
  }
}
