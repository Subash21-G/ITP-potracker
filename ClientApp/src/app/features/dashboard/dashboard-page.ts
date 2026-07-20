import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import {
  DashboardResponse,
} from '../../core/models/dashboard.model';
import {
  DashboardApiService,
} from '../../core/services/dashboard-api.service';

@Component({
  selector: 'app-dashboard-page',
  imports: [
    CommonModule,
    RouterLink,
  ],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage implements OnInit {
  private readonly dashboardApi =
    inject(DashboardApiService);

  readonly dashboard =
    signal<DashboardResponse | null>(null);

  readonly isLoading = signal(true);
  readonly errorMessage = signal('');

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.dashboardApi
      .getDashboard()
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.dashboard.set(response);
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) {
            this.errorMessage.set(
              'Your session has expired. Please sign in again.',
            );
            return;
          }

          if (error.status === 403) {
            this.errorMessage.set(
              'You do not have permission to view the dashboard.',
            );
            return;
          }

          this.errorMessage.set(
            'Dashboard data could not be loaded. Please try again.',
          );
        },
      });
  }

  percentage(
    value: number,
    total: number,
  ): number {
    if (total <= 0) {
      return 0;
    }

    return Math.min(
      100,
      Math.max(
        0,
        Math.round((value / total) * 100),
      ),
    );
  }

  processTotal(
    response: DashboardResponse,
  ): number {
    return (
      response.process.pending +
      response.process.inProgress +
      response.process.completed
    );
  }

  dispatchTotal(
    response: DashboardResponse,
  ): number {
    return (
      response.dispatch.pending +
      response.dispatch.partial +
      response.dispatch.completed
    );
  }

  invoiceTotal(
    response: DashboardResponse,
  ): number {
    return (
      response.invoice.notInvoiced +
      response.invoice.partiallyInvoiced +
      response.invoice.fullyInvoiced
    );
  }

  statusClass(status: string): string {
    return status
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-');
  }
}
