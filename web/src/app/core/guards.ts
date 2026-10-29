import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Signed-out visitors go to /login?next=<this page>. */
export const signedInGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isSignedIn() || inject(Router).createUrlTree(['/login'], { queryParams: { next: state.url } });

export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (!auth.isSignedIn()) {
    return inject(Router).createUrlTree(['/login'], { queryParams: { next: state.url } });
  }
  return auth.isAdmin() || inject(Router).createUrlTree(['/']);
};

/** Login and register pages send signed-in users to the shop. */
export const signedOutGuard: CanActivateFn = () => !inject(AuthService).isSignedIn() || inject(Router).createUrlTree(['/']);
