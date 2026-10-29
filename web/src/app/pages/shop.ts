import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CartService } from '../core/cart.service';
import { errorMessage, formatMoney } from '../core/format';
import { ProductPage } from '../core/models';
import { Pager } from '../shared/pager';

@Component({
  selector: 'app-shop',
  imports: [FormsModule, Pager],
  template: `
    <h1>Shop</h1>
    <form class="search" role="search" (ngSubmit)="load(1)">
      <label for="search" class="visually-hidden">Search products</label>
      <input id="search" name="search" type="search" placeholder="Search by name or SKU" [(ngModel)]="search" />
      <button class="button" type="submit">Search</button>
    </form>

    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    } @else if (!page()) {
      <p class="muted" aria-live="polite">Loading products...</p>
    } @else if (page()!.items.length === 0) {
      <p class="empty">No products match "{{ search }}".</p>
    } @else {
      <ul class="product-grid">
        @for (product of page()!.items; track product.id) {
          <li class="card product">
            <h2>{{ product.name }}</h2>
            <p class="muted small">{{ product.sku }}</p>
            <p>{{ product.description }}</p>
            <p class="price">{{ money(product.priceCents) }}</p>
            <p class="small" [class.warn]="product.stock > 0 && product.stock <= 5">
              {{ product.stock === 0 ? 'Out of stock' : product.stock <= 5 ? 'Only ' + product.stock + ' left' : 'In stock' }}
            </p>
            <button type="button" class="button" [disabled]="product.stock === 0" (click)="add(product)">
              {{ justAdded() === product.id ? 'Added' : 'Add to cart' }}
            </button>
          </li>
        }
      </ul>
      <app-pager [page]="page()!.page" [pageSize]="page()!.pageSize" [total]="page()!.total" (changed)="load($event)" />
    }
  `,
})
export class ShopPage {
  private readonly http = inject(HttpClient);
  private readonly cart = inject(CartService);
  protected readonly page = signal<ProductPage | null>(null);
  protected readonly error = signal('');
  protected readonly justAdded = signal<string | null>(null);
  protected search = '';
  protected readonly money = formatMoney;

  constructor() {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    this.error.set('');
    try {
      this.page.set(await firstValueFrom(this.http.get<ProductPage>('/api/catalog/products', {
        params: { page, pageSize: 12, ...(this.search.trim() ? { search: this.search.trim() } : {}) },
      })));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected add(product: Parameters<CartService['add']>[0]): void {
    this.cart.add(product);
    this.justAdded.set(product.id);
    setTimeout(() => this.justAdded.set(null), 1200);
  }
}
