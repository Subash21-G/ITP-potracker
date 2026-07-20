import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  ActivatedRoute,
  Router,
  RouterLink,
} from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize, switchMap } from 'rxjs';
import { AuthApiService } from '../../../core/services/auth-api.service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.html',
  styleUrl: './login-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly isSubmitting = signal(false);
  readonly showPassword = signal(false);
  readonly errorMessage = signal('');

  readonly loginForm = this.formBuilder.nonNullable.group({
    userName: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  ngOnInit(): void {
    if (
      this.authApi.authenticationChecked() &&
      this.authApi.isAuthenticated()
    ) {
      void this.router.navigateByUrl(this.getSafeReturnUrl());
      return;
    }

    if (!this.authApi.authenticationChecked()) {
      this.authApi.getCurrentUser().subscribe({
        next: (user) => {
          if (user.isAuthenticated) {
            void this.router.navigateByUrl(
              this.getSafeReturnUrl(),
            );
          }
        },
        error: () => {
          this.authApi.clearLocalAuthentication();
        },
      });
    }
  }

  submit(): void {
    this.errorMessage.set('');

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);

    this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.authApi.login(
            this.loginForm.getRawValue(),
          ),
        ),
        finalize(() => {
          this.isSubmitting.set(false);
        }),
      )
      .subscribe({
        next: (response) => {
          if (!response.success || !response.user) {
            this.errorMessage.set(
              response.message ||
                'Unable to sign in. Please try again.',
            );
            return;
          }

          void this.router.navigateByUrl(
            this.getSafeReturnUrl(),
          );
        },
        error: (error: HttpErrorResponse) => {
          const apiMessage =
            typeof error.error?.message === 'string'
              ? error.error.message
              : '';

          if (error.status === 400 || error.status === 401) {
            this.errorMessage.set(
              apiMessage ||
                'Invalid username or password.',
            );
            return;
          }

          this.errorMessage.set(
            apiMessage ||
              'Unable to connect to PO Tracker. Please try again.',
          );
        },
      });
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((value) => !value);
  }

  private getSafeReturnUrl(): string {
    const returnUrl =
      this.route.snapshot.queryParamMap.get(
        'returnUrl',
      ) ?? '/dashboard';

    const isSafeLocalUrl =
      returnUrl.startsWith('/') &&
      !returnUrl.startsWith('//') &&
      !returnUrl.includes('://');

    return isSafeLocalUrl
      ? returnUrl
      : '/dashboard';
  }
}
