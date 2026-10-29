import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { errorMessage, formatDateTime, formatMoney } from '../core/format';
import { Order } from '../core/models';
import { LivePoll, livePoll } from '../shared/live-poll';
import { StatusBadge } from '../shared/status-badge';

@Component({
  selector: 'app-order-detail',
  imports: [RouterLink, StatusBadge],
  template: `
    <p><a [routerLink]="auth.isAdmin() ? '/admin/orders' : '/orders'" class="text-link">Back to orders</a></p>
    <h1>Order details</h1>
    @if (loadError()) {
      <p class="alert error" role="alert">{{ loadError() }}</p>
    } @else if (!order()) {
      <p class="muted" aria-live="polite">Loading order...</p>
    } @else {
      <div class="card">
        <p aria-live="polite">Status: <app-status-badge [status]="order()!.status" /></p>
        @if (order()!.status === 'Pending') {
          <p class="muted small">Reserving stock. This page updates by itself.</p>
        }
        @if (order()!.statusReason) {
          <p>{{ order()!.statusReason }}</p>
        }
        <p class="muted small">Placed {{ date(order()!.createdAt) }} by {{ order()!.customerEmail }}. Order {{ order()!.id }}</p>
      </div>
      <table class="table">
        <thead><tr><th scope="col">Product</th><th scope="col">Price</th><th scope="col">Quantity</th><th scope="col">Total</th></tr></thead>
        <tbody>
          @for (line of order()!.lines; track line.productId) {
            <tr><td>{{ line.productName }}</td><td>{{ money(line.unitPriceCents) }}</td><td>{{ line.quantity }}</td><td>{{ money(line.lineTotalCents) }}</td></tr>
          }
        </tbody>
      </table>
      <p class="total">Total: <strong>{{ money(order()!.totalCents) }}</strong></p>

      @if (actionError()) {
        <p class="alert error" role="alert">{{ actionError() }}</p>
      }
      @if (order()!.status === 'Confirmed') {
        <button type="button" class="button danger" [disabled]="cancelling()" (click)="cancel()">
          {{ cancelling() ? 'Cancelling...' : 'Cancel order' }}
        </button>
      }
    }
  `,
})
export class OrderDetailPage {
  readonly id = input.required<string>();
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);
  protected readonly order = signal<Order | null>(null);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  protected readonly cancelling = signal(false);
  protected readonly money = formatMoney;
  protected readonly date = formatDateTime;

  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    // Effects first run after inputs are set, and again whenever the route's id changes: Angular reuses
    // this page when going from one order to another, so the old order must be cleared and reloaded.
    let poll: LivePoll | undefined;
    effect(() => {
      this.id();
      untracked(() => {
        this.order.set(null);
        this.loadError.set('');
        this.actionError.set('');
        if (poll) {
          poll.refresh();
        } else {
          poll = livePoll(this.destroyRef, () => this.load(), () => this.order()?.status === 'Pending');
        }
      });
    });
  }

  protected async cancel(): Promise<void> {
    if (!confirm('Are you sure you want to cancel this order? The items go back into stock.')) {
      return;
    }
    this.cancelling.set(true);
    this.actionError.set('');
    try {
      this.order.set(await firstValueFrom(this.http.post<Order>(`/api/orders/${this.id()}/cancel`, null)));
    } catch (e) {
      this.actionError.set(errorMessage(e));
    } finally {
      this.cancelling.set(false);
    }
  }

  private async load(): Promise<void> {
    const id = this.id();
    try {
      const order = await firstValueFrom(this.http.get<Order>(`/api/orders/${id}`));
      if (id === this.id()) {
        this.order.set(order);
      }
    } catch (e) {
      if (id === this.id()) {
        this.loadError.set(errorMessage(e));
      }
    }
  }
}
