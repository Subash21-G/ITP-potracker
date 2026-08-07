import { DOCUMENT } from '@angular/common';

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
  filter,
  finalize,
  map,
  switchMap,
  tap,
} from 'rxjs';

import {
  AuthApiService,
} from './core/services/auth-api.service';

import {
  ToastService,
} from './core/services/toast.service';

import {
  ExcelTableEnhancerService,
} from './core/services/excel-table-enhancer.service';

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
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly router =
    inject(Router);

  private readonly document =
    inject(DOCUMENT);

  private readonly authApi =
    inject(AuthApiService);

  private readonly toastService =
    inject(ToastService);

  private readonly excelTableEnhancer =
    inject(
      ExcelTableEnhancerService,
    );

  readonly currentUser =
    this.authApi.currentUser;

  readonly isLoggingOut =
    signal(false);

  readonly isMobileMenuOpen =
    signal(false);

  private readonly currentUrl =
    toSignal(
      this.router.events.pipe(
        filter(
          (event): event is NavigationEnd =>
            event instanceof NavigationEnd,
        ),
        tap(() =>
          this.closeNavigation()),
        map(
          (event) =>
            event.urlAfterRedirects,
        ),
      ),
      {
        initialValue: this.router.url,
      },
    );

  readonly isAuthenticationPage =
    computed(() =>
      this.currentUrl().startsWith(
        '/login',
      ) ||
      this.currentUrl().startsWith(
        '/register',
      ),
    );

  constructor() {
    this.excelTableEnhancer.start();
  }

  readonly userInitials =
    computed(() => {
      const user =
        this.currentUser();

      const displayName =
        user?.fullName?.trim() ||
        user?.userName?.trim() ||
        'User';

      return displayName
        .split(/\s+/)
        .filter(
          (word) =>
            word.length > 0,
        )
        .slice(0, 2)
        .map(
          (word) =>
            word
              .charAt(0)
              .toUpperCase(),
        )
        .join('');
    });

  toggleMobileMenu(): void {
    this.isMobileMenuOpen.update(
      (isOpen) =>
        !isOpen,
    );
  }

  prepareNavigationGroup(
    event: Event,
  ): void {
    const eventTarget =
      event.currentTarget;

    const currentDetails =
      eventTarget instanceof HTMLElement
        ? eventTarget.closest(
            'details',
          )
        : null;

    this.document
      .querySelectorAll<
        HTMLDetailsElement
      >(
        '.nav-group[open], .user-menu[open]',
      )
      .forEach(
        (details) => {
          if (
            details !==
            currentDetails
          ) {
            details.removeAttribute(
              'open',
            );
          }
        },
      );
  }

  closeNavigation(): void {
    this.isMobileMenuOpen.set(
      false,
    );

    this.document
      .querySelectorAll<
        HTMLDetailsElement
      >(
        '.nav-group[open], .user-menu[open]',
      )
      .forEach(
        (details) =>
          details.removeAttribute(
            'open',
          ),
      );
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
          () =>
            this.authApi.logout(),
        ),
        finalize(() => {
          this.isLoggingOut.set(
            false,
          );
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
