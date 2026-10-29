import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <h1>Page not found</h1>
    <p>That page doesn't exist. <a routerLink="/" class="text-link">Go to the shop</a>.</p>
  `,
})
export class NotFoundPage {}
