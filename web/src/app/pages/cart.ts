import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { CartService, MAX_QUANTITY } from '../core/cart.service';
import { errorMessage, formatMoney } from '../core/format';
import { Order } from '../core/models';

@Component({
  selector: 'app-cart',
  imports: [RouterLink],
  template: `
    <h1>Cart</h1>
    @if (cart.lines().length === 0) {
      <p class="empty">Your cart is empty. <a routerLink="/" class="text-link">Browse the shop</a>.</p>
    } @else {
      <table class="table">
        <thead>
          <tr><th scope="col">Product</th><th scope="col">Price</th><th scope="col">Quantity</th><th scope="col">Total</th><th scope="col"><span class="visually-hidden">Actions</span></th></tr>
        </thead>
        <tbody>
          @for (line of cart.lines(); track line.productId) {
            <tr>
              <td>{{ line.name }}</td>
              <td>{{ money(line.priceCents) }}</td>
              <td>
                <label class="visually-hidden" [for]="'qty-' + line.productId">Quantity of {{ line.name }}</label>
                <input class="qty" type="number" min="1" [max]="max" [id]="'qty-' + line.productId" [value]="line.quantity"
                  (change)="cart.setQuantity(line.productId, +$any($event.target).value)" />
              </td>
              <td>{{ money(line.priceCents * line.quantity) }}</td>
              <td><button type="button" class="link-button" (click)="cart.remove(line.productId)">Remove</button></td>
            </tr>
          }
        </tbody>
      </table>
      <p class="total">Total: <strong>{{ money(cart.totalCents()) }}</strong></p>
      <p class="muted small">Prices are confirmed when you order; stock is reserved within a few seconds.</p>

      @if (error()) {
        <p class="alert error" role="alert">{{ error() }}</p>
      }
      @if (auth.isSignedIn()) {
        <button type="button" class="button" [disabled]="placing()" (click)="place()">{{ placing() ? 'Placing order...' : 'Place order' }}</button>
      } @else {
        <a class="button" routerLink="/login" [queryParams]="{ next: '/cart' }">Sign in to order</a>
      }
    }
  `,
})
export class CartPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  protected readonly cart = inject(CartService);
  protected readonly auth = inject(AuthService);
  protected readonly placing = signal(false);
  protected readonly error = signal('');
  protected readonly money = formatMoney;
  protected readonly max = MAX_QUANTITY;

  protected async place(): Promise<void> {
    this.placing.set(true);
    this.error.set('');
    try {
      const lines = this.cart.lines().map(l => ({ productId: l.productId, quantity: l.quantity }));
      const order = await firstValueFrom(this.http.post<Order>('/api/orders', { lines }));
      this.cart.clear();
      await this.router.navigate(['/orders', order.id]);
    } catch (e) {
      this.error.set(errorMessage(e));
      this.placing.set(false);
    }
  }
}
