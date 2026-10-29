import { Routes } from '@angular/router';
import { adminGuard, signedInGuard, signedOutGuard } from './core/guards';

export const routes: Routes = [
  { path: '', title: 'Shop | OrderFlow', loadComponent: () => import('./pages/shop').then(m => m.ShopPage) },
  { path: 'cart', title: 'Cart | OrderFlow', loadComponent: () => import('./pages/cart').then(m => m.CartPage) },
  { path: 'login', title: 'Sign in | OrderFlow', canActivate: [signedOutGuard], loadComponent: () => import('./pages/login').then(m => m.LoginPage) },
  { path: 'register', title: 'Create account | OrderFlow', canActivate: [signedOutGuard], loadComponent: () => import('./pages/register').then(m => m.RegisterPage) },
  { path: 'orders', title: 'My orders | OrderFlow', canActivate: [signedInGuard], loadComponent: () => import('./pages/orders').then(m => m.OrdersPage) },
  { path: 'orders/:id', title: 'Order | OrderFlow', canActivate: [signedInGuard], loadComponent: () => import('./pages/order-detail').then(m => m.OrderDetailPage) },
  { path: 'settings', title: 'Settings | OrderFlow', loadComponent: () => import('./pages/settings').then(m => m.SettingsPage) },
  { path: 'admin/products', title: 'Products | OrderFlow', canActivate: [adminGuard], loadComponent: () => import('./pages/admin/admin-products').then(m => m.AdminProductsPage) },
  { path: 'admin/orders', title: 'All orders | OrderFlow', canActivate: [adminGuard], data: { all: true }, loadComponent: () => import('./pages/orders').then(m => m.OrdersPage) },
  { path: '**', title: 'Not found | OrderFlow', loadComponent: () => import('./pages/not-found').then(m => m.NotFoundPage) },
];
