import { CartService, MAX_QUANTITY } from './cart.service';
import { Product } from './models';

const mouse: Product = { id: 'p1', sku: 'MS-01', name: 'Mouse', description: '', priceCents: 2900, stock: 9, isActive: true, updatedAt: '' };

describe('CartService', () => {
  beforeEach(() => localStorage.clear());

  it('adds, merges and totals lines, and remembers them', () => {
    const cart = new CartService();
    cart.add(mouse);
    cart.add(mouse);

    expect(cart.count()).toBe(2);
    expect(cart.totalCents()).toBe(5800);
    expect(new CartService().lines()).toEqual(cart.lines());
  });

  it('keeps quantities between 1 and the maximum', () => {
    const cart = new CartService();
    cart.add(mouse);
    cart.setQuantity('p1', 0);
    expect(cart.lines()[0].quantity).toBe(1);
    cart.setQuantity('p1', 5000);
    expect(cart.lines()[0].quantity).toBe(MAX_QUANTITY);
  });

  it('survives broken saved data', () => {
    localStorage.setItem('orderflow.cart', '{not json');
    expect(new CartService().lines()).toEqual([]);
  });
});
