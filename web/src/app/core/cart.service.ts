import { Injectable, computed, signal } from '@angular/core';
import { Product } from './models';

export interface CartLine {
  productId: string;
  name: string;
  priceCents: number;
  quantity: number;
}

const STORAGE_KEY = 'orderflow.cart';
export const MAX_QUANTITY = 100;

@Injectable({ providedIn: 'root' })
export class CartService {
  readonly lines = signal<CartLine[]>(readCart());
  readonly count = computed(() => this.lines().reduce((sum, l) => sum + l.quantity, 0));
  readonly totalCents = computed(() => this.lines().reduce((sum, l) => sum + l.priceCents * l.quantity, 0));

  add(product: Product): void {
    const existing = this.lines().find(l => l.productId === product.id);
    this.save(
      existing
        ? this.lines().map(l => (l === existing ? { ...l, quantity: Math.min(l.quantity + 1, MAX_QUANTITY) } : l))
        : [...this.lines(), { productId: product.id, name: product.name, priceCents: product.priceCents, quantity: 1 }],
    );
  }

  setQuantity(productId: string, quantity: number): void {
    const clamped = Math.max(1, Math.min(MAX_QUANTITY, Math.floor(quantity) || 1));
    this.save(this.lines().map(l => (l.productId === productId ? { ...l, quantity: clamped } : l)));
  }

  remove(productId: string): void {
    this.save(this.lines().filter(l => l.productId !== productId));
  }

  clear(): void {
    this.save([]);
  }

  private save(lines: CartLine[]): void {
    this.lines.set(lines);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(lines));
    } catch {
      // Storage full or blocked: the cart still works for this visit.
    }
  }
}

function readCart(): CartLine[] {
  try {
    const lines = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
    return Array.isArray(lines) ? lines : [];
  } catch {
    return [];
  }
}
