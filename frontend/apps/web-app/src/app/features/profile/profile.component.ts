import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiClient } from '@SearchConsoleApp/shared/core';
import { Customer } from '@SearchConsoleApp/shared/models';

@Component({
  selector: 'SearchConsoleApp-profile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    <section class="surface" style="padding: var(--space-6)">
      <h2>Profilim</h2>
      @if (profile(); as p) {
        <dl>
          <dt>E-posta</dt><dd>{{ p.email }}</dd>
          <dt>Ad Soyad</dt><dd>{{ p.firstName }} {{ p.lastName }}</dd>
          <dt>Üyelik</dt><dd>{{ p.createdOnUtc | date:'mediumDate' }}</dd>
        </dl>
      } @else {
        <p>Yükleniyor…</p>
      }
    </section>
  `,
  styles: [`
    dl { display: grid; grid-template-columns: 160px 1fr; gap: var(--space-2) var(--space-4); }
    dt { color: var(--color-text-muted); font-weight: 500; }
    dd { margin: 0; color: var(--color-text); }
  `]
})
export class ProfileComponent implements OnInit {
  private api = inject(ApiClient);
  profile = signal<Customer | null>(null);

  ngOnInit() {
    this.api.get<Customer>('me').subscribe(p => this.profile.set(p));
  }
}
