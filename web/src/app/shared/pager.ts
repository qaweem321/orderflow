import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pager',
  template: `
    @if (pages() > 1) {
      <nav class="pager" aria-label="Pages">
        <button type="button" class="button secondary" [disabled]="page() <= 1" (click)="changed.emit(page() - 1)">Previous</button>
        <span>Page {{ page() }} of {{ pages() }}</span>
        <button type="button" class="button secondary" [disabled]="page() >= pages()" (click)="changed.emit(page() + 1)">Next</button>
      </nav>
    }
  `,
})
export class Pager {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly changed = output<number>();
  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));
}
