import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { errorMessage, formatDateTime, formatMoney } from '../core/format';
import { OrderPage, OrderStatus } from '../core/models';
import { Pager } from '../shared/pager';
import { StatusBadge } from '../shared/status-badge';
import { livePoll } from '../shared/live-poll';

/** Your orders, or every order for admins (route data all = true). Refreshes itself while any order is pending. */
@Component({
  selector: 'app-orders',
  imports: [RouterLink, StatusBadge, Pager],
  template: `
    <h1>{{ all() ? 'All orders' : 'My orders' }}</h1>
    @if (all()) {
      <label for="status">Status</label>
      <select id="status" class="inline-select" [value]="status()" (change)="filter($any($event.target).value)">
        <option value="">All</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    }
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    } @else if (!page()) {
      <p class="muted" aria-live="polite">Loading orders...</p>
    } @else if (page()!.items.length === 0) {
      <p class="empty">No orders yet. <a routerLink="/" class="text-link">Start shopping</a>.</p>
    } @else {
      <table class="table">
        <thead>
          <tr><th scope="col">Placed</th>@if (all()) {<th scope="col">Customer</th>}<th scope="col">Items</th><th scope="col">Total</th><th scope="col">Status</th></tr>
        </thead>
        <tbody>
          @for (order of page()!.items; track order.id) {
            <tr>
              <td><a [routerLink]="['/orders', order.id]">{{ date(order.createdAt) }}</a></td>
              @if (all()) {<td>{{ order.customerEmail }}</td>}
              <td>{{ order.lines.length }}</td>
              <td>{{ money(order.totalCents) }}</td>
              <td><app-status-badge [status]="order.status" /></td>
            </tr>
          }
        </tbody>
      </table>
      <app-pager [page]="page()!.page" [pageSize]="page()!.pageSize" [total]="page()!.total" (changed)="go($event)" />
    }
  `,
})
export class OrdersPage implements OnInit {
  readonly all = input(false);
  private readonly http = inject(HttpClient);
  protected readonly page = signal<OrderPage | null>(null);
  protected readonly error = signal('');
  protected readonly status = signal<OrderStatus | ''>('');
  protected readonly statuses: OrderStatus[] = ['Pending', 'Confirmed', 'Rejected', 'Cancelled'];
  protected readonly money = formatMoney;
  protected readonly date = formatDateTime;
  private current = 1;

  private readonly destroyRef = inject(DestroyRef);

  // Start in ngOnInit, not the constructor: route inputs (id, all) are not set yet in the constructor.
  ngOnInit(): void {
    livePoll(this.destroyRef, () => this.load(), () => this.page()?.items.some(o => o.status === 'Pending') ?? false);
  }

  protected go(page: number): void {
    this.current = page;
    void this.load();
  }

  protected filter(status: OrderStatus | ''): void {
    this.status.set(status);
    this.go(1);
  }

  private async load(): Promise<void> {
    try {
      const params: Record<string, string | number | boolean> = { page: this.current, pageSize: 20, all: this.all() };
      if (this.status()) {
        params['status'] = this.status();
      }
      this.page.set(await firstValueFrom(this.http.get<OrderPage>('/api/orders', { params })));
      this.error.set('');
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
