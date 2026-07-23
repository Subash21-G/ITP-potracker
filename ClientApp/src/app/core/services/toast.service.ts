import {
  Injectable,
  signal,
} from '@angular/core';

export type ToastType =
  | 'success'
  | 'error'
  | 'warning'
  | 'info';

export interface ToastMessage {
  id: number;
  type: ToastType;
  title: string;
  message: string;
  duration: number;
}

@Injectable({
  providedIn: 'root',
})
export class ToastService {
  private nextId = 1;

  private readonly toastsState =
    signal<ToastMessage[]>([]);

  readonly toasts =
    this.toastsState.asReadonly();

  success(
    title: string,
    message: string,
    duration = 3500,
  ): void {
    this.show(
      'success',
      title,
      message,
      duration,
    );
  }

  error(
    title: string,
    message: string,
    duration = 5000,
  ): void {
    this.show(
      'error',
      title,
      message,
      duration,
    );
  }

  warning(
    title: string,
    message: string,
    duration = 4500,
  ): void {
    this.show(
      'warning',
      title,
      message,
      duration,
    );
  }

  info(
    title: string,
    message: string,
    duration = 3500,
  ): void {
    this.show(
      'info',
      title,
      message,
      duration,
    );
  }

  remove(id: number): void {
    this.toastsState.update(
      (items) =>
        items.filter(
          (item) => item.id !== id,
        ),
    );
  }

  clear(): void {
    this.toastsState.set([]);
  }

  private show(
    type: ToastType,
    title: string,
    message: string,
    duration: number,
  ): void {
    const id = this.nextId++;

    this.toastsState.update(
      (items) => [
        ...items,
        {
          id,
          type,
          title,
          message,
          duration,
        },
      ],
    );

    window.setTimeout(
      () => this.remove(id),
      duration,
    );
  }
}
