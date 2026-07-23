import {
  Injectable,
  computed,
  signal,
} from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LoadingService {
  private readonly activeRequestsState =
    signal(0);

  readonly isLoading = computed(
    () => this.activeRequestsState() > 0,
  );

  show(): void {
    this.activeRequestsState.update(
      (count) => count + 1,
    );
  }

  hide(): void {
    this.activeRequestsState.update(
      (count) => Math.max(0, count - 1),
    );
  }

  reset(): void {
    this.activeRequestsState.set(0);
  }
}
