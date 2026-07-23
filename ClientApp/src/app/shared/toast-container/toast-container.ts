import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';

import {
  ToastService,
} from '../../core/services/toast.service';

@Component({
  selector: 'app-toast-container',
  templateUrl: './toast-container.html',
  styleUrl: './toast-container.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastContainer {
  private readonly toastService =
    inject(ToastService);

  readonly toasts =
    this.toastService.toasts;

  remove(id: number): void {
    this.toastService.remove(id);
  }
}
