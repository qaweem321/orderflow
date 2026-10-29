import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { errorMessage, formatMoney } from '../../core/format';
import { Product, ProductPage } from '../../core/models';
import { Pager } from '../../shared/pager';

interface Draft {
  id: string | null;
  sku: string;
  name: string;
  description: string;
  price: number;
  stock: number;
  isActive: boolean;
}

const EMPTY: Draft = { id: null, sku: '', name: '', description: '', price: 10, stock: 0, isActive: true };

@Component({
  selector: 'app-admin-products',
  imports: [FormsModule, Pager],
  template: `
    <h1>Products</h1>
    <section class="card">
      <h2>{{ draft.id ? 'Edit ' + draft.name : 'New product' }}</h2>
      <form class="grid-form" (ngSubmit)="save()">
        <div><label for="sku">SKU</label>
          <input id="sku" name="sku" required pattern="[A-Za-z0-9-]+" [disabled]="!!draft.id" [(ngModel)]="draft.sku" /></div>
        <div><label for="pname">Name</label><input id="pname" name="pname" required [(ngModel)]="draft.name" /></div>
        <div><label for="price">Price (USD)</label><input id="price" name="price" type="number" min="0.01" step="0.01" required [(ngModel)]="draft.price" /></div>
        <div><label for="stock">Stock</label><input id="stock" name="stock" type="number" min="0" step="1" required [(ngModel)]="draft.stock" /></div>
        <div class="wide"><label for="desc">Description</label><textarea id="desc" name="desc" rows="2" [(ngModel)]="draft.description"></textarea></div>
        @if (draft.id) {
          <label class="check"><input type="checkbox" name="active" [(ngModel)]="draft.isActive" /> For sale</label>
        }
        @if (formError()) {
          <p class="alert error wide" role="alert">{{ formError() }}</p>
        }
        <div class="actions wide">
          <button class="button" type="submit" [disabled]="saving()">{{ saving() ? 'Saving...' : 'Save' }}</button>
          @if (draft.id) {
            <button class="button secondary" type="button" (click)="reset()">Cancel editing</button>
          }
        </div>
      </form>
    </section>

    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    } @else if (!page()) {
      <p class="muted" aria-live="polite">Loading products...</p>
    } @else {
      <table class="table">
        <thead><tr><th scope="col">SKU</th><th scope="col">Name</th><th scope="col">Price</th><th scope="col">Stock</th><th scope="col">For sale</th><th scope="col"><span class="visually-hidden">Actions</span></th></tr></thead>
        <tbody>
          @for (product of page()!.items; track product.id) {
            <tr>
              <td>{{ product.sku }}</td><td>{{ product.name }}</td><td>{{ money(product.priceCents) }}</td>
              <td>{{ product.stock }}</td><td>{{ product.isActive ? 'Yes' : 'No' }}</td>
              <td><button type="button" class="link-button" (click)="edit(product)">Edit<span class="visually-hidden"> {{ product.name }}</span></button></td>
            </tr>
          }
        </tbody>
      </table>
      <app-pager [page]="page()!.page" [pageSize]="page()!.pageSize" [total]="page()!.total" (changed)="load($event)" />
    }
  `,
})
export class AdminProductsPage {
  private readonly http = inject(HttpClient);
  protected readonly page = signal<ProductPage | null>(null);
  protected readonly error = signal('');
  protected readonly formError = signal('');
  protected readonly saving = signal(false);
  protected draft: Draft = { ...EMPTY };
  protected readonly money = formatMoney;

  constructor() {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    try {
      this.page.set(await firstValueFrom(this.http.get<ProductPage>('/api/catalog/products', { params: { page, pageSize: 20, includeInactive: true } })));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected edit(p: Product): void {
    this.draft = { id: p.id, sku: p.sku, name: p.name, description: p.description, price: p.priceCents / 100, stock: p.stock, isActive: p.isActive };
    this.formError.set('');
    scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected reset(): void {
    this.draft = { ...EMPTY };
    this.formError.set('');
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    this.formError.set('');
    const d = this.draft;
    const priceCents = Math.round(d.price * 100);
    try {
      if (d.id) {
        await firstValueFrom(this.http.put(`/api/catalog/products/${d.id}`, { name: d.name, description: d.description, priceCents, isActive: d.isActive }));
        await firstValueFrom(this.http.put(`/api/catalog/products/${d.id}/stock`, { stock: d.stock }));
      } else {
        await firstValueFrom(this.http.post('/api/catalog/products', { sku: d.sku, name: d.name, description: d.description, priceCents, stock: d.stock }));
      }
      this.reset();
      await this.load(this.page()?.page ?? 1);
    } catch (e) {
      this.formError.set(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
}
