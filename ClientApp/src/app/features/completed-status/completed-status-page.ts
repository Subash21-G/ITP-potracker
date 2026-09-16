import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardResponse } from '../../core/models/dashboard.model';
import { DashboardApiService } from '../../core/services/dashboard-api.service';

@Component({
  selector: 'app-completed-status-page',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './completed-status-page.html',
  styleUrl: './completed-status-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompletedStatusPage {
  private readonly api = inject(DashboardApiService);
  readonly data = signal<DashboardResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly completedMaterials = computed(() =>
    (this.data()?.recentMaterials ?? []).filter(material =>
      material.processStatus.trim().toLowerCase() === 'completed'
      || material.invoiceStatus.trim().toLowerCase() === 'fully invoiced',
    ),
  );
  readonly pageSize = signal(10);
  readonly pageSizes = [5, 10, 25, 50];
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.completedMaterials().length / this.pageSize())));
  readonly page = linkedSignal<number, number>({ source: this.totalPages, computation: (total, previous) => Math.min(previous?.value ?? 1, total) });
  readonly pagedCompletedMaterials = computed(() => this.completedMaterials().slice((this.page() - 1) * this.pageSize(), this.page() * this.pageSize()));

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.getDashboard(true).subscribe({
      next: response => { this.data.set(response); this.loading.set(false); },
      error: () => { this.error.set('Completion data could not be loaded.'); this.loading.set(false); },
    });
  }

  changePageSize(value: string): void {
    const size = Number(value);
    if (!this.pageSizes.includes(size)) return;
    this.pageSize.set(size);
    this.page.set(1);
  }
}
