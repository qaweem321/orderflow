export type Role = 'admin' | 'customer';

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: Role;
}

export interface Session {
  token: string;
  expiresAt: string;
  user: User;
}

export interface Product {
  id: string;
  sku: string;
  name: string;
  description: string;
  priceCents: number;
  stock: number;
  isActive: boolean;
  updatedAt: string;
}

export interface ProductPage {
  items: Product[];
  total: number;
  page: number;
  pageSize: number;
}

export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'Cancelled';

export interface OrderLine {
  productId: string;
  productName: string;
  unitPriceCents: number;
  quantity: number;
  lineTotalCents: number;
}

export interface Order {
  id: string;
  customerEmail: string;
  status: OrderStatus;
  statusReason: string | null;
  totalCents: number;
  createdAt: string;
  updatedAt: string;
  lines: OrderLine[];
}

export interface OrderPage {
  items: Order[];
  total: number;
  page: number;
  pageSize: number;
}
