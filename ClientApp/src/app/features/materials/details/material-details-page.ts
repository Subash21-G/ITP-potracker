import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import {
  MaterialDetails,
} from '../../../core/models/material-crud.model';
import {
  MaterialCrudApiService,
} from '../../../core/services/material-crud-api.service';

@Component({
  selector: 'app-material-details-page',
  imports: [
    CommonModule,
    RouterLink,
  ],
  templateUrl: './material-details-page.html',
  styleUrl: './material-details-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaterialDetailsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly materialApi =
    inject(MaterialCrudApiService);

  readonly material =
    signal<MaterialDetails | null>(null);

  readonly isLoading = signal(true);
  readonly isDeleting = signal(false);
  readonly errorMessage = signal('');
  readonly actionMessage = signal('');

  ngOnInit(): void {
    const id = Number(
      this.route.snapshot.paramMap.get('id'),
    );

    if (!Number.isInteger(id) || id <= 0) {
      this.errorMessage.set(
        'Invalid material number.',
      );
      this.isLoading.set(false);
      return;
    }

    this.loadMaterial(id);
  }

  loadMaterial(id?: number): void {
    const materialId =
      id ?? this.material()?.id;

    if (!materialId) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');
    this.actionMessage.set('');

    this.materialApi
      .getDetails(materialId)
      .pipe(
        finalize(() => {
          this.isLoading.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          this.material.set(response);
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 404) {
            this.errorMessage.set(
              'Material was not found.',
            );
            return;
          }

          if (error.status === 401) {
            this.errorMessage.set(
              'Your session has expired. Please sign in again.',
            );
            return;
          }

          if (error.status === 403) {
            this.errorMessage.set(
              'You do not have permission to view this material.',
            );
            return;
          }

          this.errorMessage.set(
            'Material details could not be loaded.',
          );
        },
      });
  }

  deleteMaterial(): void {
    const current = this.material();

    if (!current || this.isDeleting()) {
      return;
    }

    if (!current.canDelete) {
      this.actionMessage.set(
        'This material cannot be deleted because invoice or dispatch records are linked.',
      );
      return;
    }

    const confirmed = window.confirm(
      `Delete material ${current.materialNo || current.id}?`,
    );

    if (!confirmed) {
      return;
    }

    this.isDeleting.set(true);
    this.actionMessage.set('');

    this.materialApi
      .delete(current.id)
      .pipe(
        finalize(() => {
          this.isDeleting.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          window.alert(response.message);
          void this.router.navigateByUrl(
            '/materials',
          );
        },
        error: (error: HttpErrorResponse) => {
          const message =
            typeof error.error?.message === 'string'
              ? error.error.message
              : '';

          if (error.status === 409) {
            this.actionMessage.set(
              message ||
                'This material has linked records and cannot be deleted.',
            );
            return;
          }

          if (error.status === 403) {
            this.actionMessage.set(
              'You do not have permission to delete materials.',
            );
            return;
          }

          this.actionMessage.set(
            message ||
              'Material could not be deleted.',
          );
        },
      });
  }

  statusClass(status: string): string {
    return status
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-');
  }

  fileName(path: string): string {
    if (!path) {
      return '';
    }

    const parts = path.split('/');

    return parts[parts.length - 1] || path;
  }
}
