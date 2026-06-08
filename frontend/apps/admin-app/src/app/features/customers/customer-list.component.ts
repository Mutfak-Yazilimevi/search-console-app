import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiClient } from '@SearchConsoleApp/shared/core';
import { Customer } from '@SearchConsoleApp/shared/models';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';

@Component({
  selector: 'SearchConsoleApp-customer-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ButtonComponent],
  template: `
    <section>
      <header class="title">
        <h2>Müşteriler</h2>
        <SearchConsoleApp-button variant="primary" size="sm">Yeni Müşteri</SearchConsoleApp-button>
      </header>

      @if (customers().length > 0) {
        <table class="surface">
          <thead>
            <tr><th>E-posta</th><th>Ad</th><th>Üyelik</th><th>Durum</th><th></th></tr>
          </thead>
          <tbody>
            @for (c of customers(); track c.entityId) {
              <tr>
                <td>{{ c.email }}</td>
                <td>{{ c.firstName }} {{ c.lastName }}</td>
                <td>{{ c.createdOnUtc | date:'mediumDate' }}</td>
                <td>{{ c.active ? 'Aktif' : 'Pasif' }}</td>
                <td>
                  <SearchConsoleApp-button variant="ghost" size="sm" (clicked)="del(c)">Sil</SearchConsoleApp-button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      } @else {
        <p>Müşteri yok.</p>
      }
    </section>
  `,
  styles: [`
    .title { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-4); }
    h2 { margin: 0; color: var(--color-text); }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: var(--space-3); text-align: left; border-bottom: 1px solid var(--color-border); }
    th { color: var(--color-text-muted); font-weight: 500; font-size: 0.875rem; }
    tbody tr:hover { background: var(--color-surface-elevated); }
  `]
})
export class CustomerListComponent implements OnInit {
  private api = inject(ApiClient);
  customers = signal<Customer[]>([]);

  ngOnInit() { this.load(); }

  load() {
    this.api.get<Customer[]>('customers').subscribe(list => this.customers.set(list));
  }

  del(c: Customer) {
    if (!confirm(`${c.email} silinsin mi?`)) return;
    this.api.delete(`customers/${c.entityId}`).subscribe(() => this.load());
  }
}
