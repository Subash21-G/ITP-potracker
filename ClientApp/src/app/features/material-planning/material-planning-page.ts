import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { finalize } from 'rxjs';

import { ManageOnlyDirective } from '../../core/directives/manage-only.directive';
import {
  MaterialPlanningItem,
  MaterialPlanningSummary,
  MaterialPlanningWorkspace,
} from '../../core/models/material-planning-api.model';
import { MaterialPlanningApiService } from '../../core/services/material-planning-api.service';

interface WorkspaceCopy {
  readonly eyebrow: string;
  readonly title: string;
  readonly description: string;
  readonly emptyMessage: string;
}

@Component({
  selector: 'app-material-planning-page',
  imports: [
    DatePipe,
    DecimalPipe,
    ManageOnlyDirective,
    ReactiveFormsModule,
    RouterLink,
    RouterLinkActive,
  ],
  templateUrl: './material-planning-page.html',
  styleUrl: './material-planning-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialPlanningPage {
  private readonly api = inject(MaterialPlanningApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly workspace = this.readWorkspace();
  readonly copy = this.workspaceCopy(this.workspace);

  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly scope = signal('');
  readonly items = signal<readonly MaterialPlanningItem[]>([]);
  readonly summary = signal<MaterialPlanningSummary>({
    totalMaterials: 0,
    inhouse: 0,
    outsourcing: 0,
    costVariation: 0,
    priority: 0,
    urgent: 0,
  });
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly totalRecords = signal(0);
  readonly totalPages = signal(1);
  readonly loading = signal(false);
  readonly errorMessage = signal('');

  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());
  readonly showingFrom = computed(() =>
    this.totalRecords() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly showingTo = computed(() =>
    Math.min(this.page() * this.pageSize(), this.totalRecords()),
  );

  constructor() {
    const requestedSearch = this.route.snapshot.queryParamMap.get('search')?.trim();
    const requestedScope = this.route.snapshot.queryParamMap.get('scope')?.trim().toLowerCase();

    if (requestedSearch) {
      this.searchControl.setValue(requestedSearch);
    }

    if (requestedScope && this.isValidScope(requestedScope)) {
      this.scope.set(requestedScope);
    }

    this.load();
  }

  applySearch(): void {
    this.page.set(1);
    this.load();
  }

  clearSearch(): void {
    this.searchControl.setValue('');
    this.page.set(1);
    this.load();
  }

  selectScope(scope: string): void {
    if (this.scope() === scope || this.loading()) {
      return;
    }

    this.scope.set(scope);
    this.page.set(1);
    this.load();
  }

  previousPage(): void {
    if (!this.hasPreviousPage() || this.loading()) {
      return;
    }

    this.page.update((value) => value - 1);
    this.load();
  }

  nextPage(): void {
    if (!this.hasNextPage() || this.loading()) {
      return;
    }

    this.page.update((value) => value + 1);
    this.load();
  }

  changePageSize(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);

    if (![10, 25, 50, 100].includes(value)) {
      return;
    }

    this.pageSize.set(value);
    this.page.set(1);
    this.load();
  }

  private load(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.api
      .getItems({
        workspace: this.workspace,
        scope: this.scope() || undefined,
        search: this.searchControl.value.trim() || undefined,
        page: this.page(),
        pageSize: this.pageSize(),
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.items.set(response.items);
          this.summary.set(response.summary);
          this.page.set(response.page);
          this.totalRecords.set(response.totalRecords);
          this.totalPages.set(response.totalPages);
        },
        error: (error: HttpErrorResponse) => {
          this.items.set([]);
          this.errorMessage.set(
            error.error?.error ?? 'The material planning list could not be loaded.',
          );
        },
      });
  }

  private readWorkspace(): MaterialPlanningWorkspace {
    const value = this.route.snapshot.data['workspace'];

    return value === 'cost-variation' || value === 'priority-urgent'
      ? value
      : 'sourcing';
  }

  private isValidScope(scope: string): boolean {
    if (this.workspace === 'sourcing') {
      return scope === 'inhouse' || scope === 'outsourcing';
    }

    if (this.workspace === 'priority-urgent') {
      return scope === 'priority' || scope === 'urgent';
    }

    return false;
  }

  private workspaceCopy(workspace: MaterialPlanningWorkspace): WorkspaceCopy {
    switch (workspace) {
      case 'cost-variation':
        return {
          eyebrow: 'Commercial control',
          title: 'Cost Variation',
          description: 'Review every material marked with a changed or exceptional cost.',
          emptyMessage: 'No materials are marked for cost variation.',
        };
      case 'priority-urgent':
        return {
          eyebrow: 'Production attention',
          title: 'Priority & Urgent',
          description: 'Keep high-priority and urgent materials visible in one action queue.',
          emptyMessage: 'No priority or urgent materials are waiting.',
        };
      default:
        return {
          eyebrow: 'Production ownership',
          title: 'Outsourcing & Inhousing',
          description: 'Separate internally produced materials from outsourced work.',
          emptyMessage: 'No materials match this sourcing view.',
        };
    }
  }
}
