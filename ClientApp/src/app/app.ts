
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';

import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';

import {
  toSignal,
} from '@angular/core/rxjs-interop';

import {
  finalize,
  filter,
  map,
  switchMap,
} from 'rxjs';

import {
  AuthApiService,
} from './core/services/auth-api.service';

import {
  LoadingService,
} from './core/services/loading.service';

import {
  ToastService,
} from './core/services/toast.service';

import {
  ToastContainer,
} from './shared/toast-container/toast-container';

@Component({
  selector: 'app-root',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ToastContainer,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly router = inject(Router);
  private readonly authApi = inject(AuthApiService);
  private readonly loadingService =
    inject(LoadingService);

  private readonly toastService =
    inject(ToastService);

  readonly currentUser =
    this.authApi.currentUser;

  readonly isLoading =
    this.loadingService.isLoading;

  readonly isLoggingOut = signal(false);

  readonly isMobileMenuOpen = signal(false);

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter(
        (event): event is NavigationEnd =>
          event instanceof NavigationEnd,
      ),
      map(
        (event) =>
          event.urlAfterRedirects,
      ),
    ),
    {
      initialValue: this.router.url,
    },
  );

  readonly isLoginPage = computed(() =>
    this.currentUrl().startsWith('/login'),
  );

  readonly userInitials = computed(() => {
    const user = this.currentUser();

    const displayName =
      user?.fullName?.trim() ||
      user?.userName?.trim() ||
      'User';

    const words = displayName
      .split(/\s+/)
      .filter(
        (word) => word.length > 0,
      )
      .slice(0, 2);

    return words
      .map(
        (word) =>
          word.charAt(0).toUpperCase(),
      )
      .join('');
  });

  toggleMobileMenu(): void {
    this.isMobileMenuOpen.update(
      (isOpen) => !isOpen,
    );
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen.set(false);
  }

  logout(): void {
    if (this.isLoggingOut()) {
      return;
    }

    this.isLoggingOut.set(true);

    this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(
          () => this.authApi.logout(),
        ),
        finalize(() => {
          this.isLoggingOut.set(false);
        }),
      )
      .subscribe({
        next: () => {
          this.toastService.success(
            'Signed out',
            'You have been logged out successfully.',
          );

          void this.router.navigate([
            '/login',
          ]);
        },
        error: () => {
          this.authApi
            .clearLocalAuthentication();

          this.toastService.warning(
            'Session closed',
            'The server logout could not be confirmed, but your local session was cleared.',
          );

          void this.router.navigate([
            '/login',
          ]);
        },
      });
  }
}




