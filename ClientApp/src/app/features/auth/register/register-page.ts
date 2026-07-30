import {
  HttpErrorResponse,
} from '@angular/common/http';

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

import {
  finalize,
  switchMap,
} from 'rxjs';

import {
  AuthApiService,
} from '../../../core/services/auth-api.service';

import {
  ToastService,
} from '../../../core/services/toast.service';

@Component({
  selector: 'app-register-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './register-page.html',
  styleUrl: '../login/login-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class RegisterPage implements OnInit {
  private readonly formBuilder =
    inject(FormBuilder);

  private readonly authApi =
    inject(AuthApiService);

  private readonly router =
    inject(Router);

  private readonly route =
    inject(ActivatedRoute);

  private readonly toastService =
    inject(ToastService);

  readonly isSubmitting =
    signal(false);

  readonly showPassword =
    signal(false);

  readonly errorMessage =
    signal('');

  readonly registerForm =
    this.formBuilder.nonNullable.group({
      fullName: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
        ],
      ],

      userName: [
        '',
        [
          Validators.required,
          Validators.minLength(4),
          Validators.maxLength(50),
          Validators.pattern(
            /^[A-Za-z0-9._-]+$/,
          ),
        ],
      ],

      password: [
        '',
        [
          Validators.required,
          Validators.minLength(8),
          Validators.pattern(
            /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/,
          ),
        ],
      ],
    });

  ngOnInit(): void {
    if (
      !this.authApi
        .authenticationChecked()
    ) {
      this.authApi
        .getCurrentUser()
        .subscribe({
          error: () => {
            this.authApi
              .clearLocalAuthentication();
          },
        });
    }
  }

  submit(): void {
    this.errorMessage.set('');

    if (this.registerForm.invalid) {
      this.registerForm
        .markAllAsTouched();

      return;
    }

    this.isSubmitting.set(
      true,
    );

    const values =
      this.registerForm
        .getRawValue();

    this.authApi
      .getXsrfToken()
      .pipe(
        switchMap(() =>
          this.authApi.register({
            fullName:
              values.fullName.trim(),

            userName:
              values.userName.trim(),

            password:
              values.password,
          }),
        ),

        finalize(() =>
          this.isSubmitting.set(
            false,
          ),
        ),
      )
      .subscribe({
        next: response => {
          if (!response.success) {
            this.errorMessage.set(
              response.message ||
                'The account could not be created.',
            );

            return;
          }

          if (
            this.authApi
              .isAuthenticated()
          ) {
            this.toastService.success(
              'User created',
              response.message,
            );

            const destination =
              this.authApi.isAdmin()
                ? this.getSafeReturnUrl()
                : '/dashboard';

            void this.router
              .navigateByUrl(
                destination,
              );

            return;
          }

          void this.router.navigate(
            [
              '/login',
            ],
            {
              queryParams: {
                registered:
                  '1',

                firstAdmin:
                  response
                    .isFirstAdministrator
                    ? '1'
                    : '0',
              },
            },
          );
        },

        error: (
          error: HttpErrorResponse,
        ) => {
          const apiMessage =
            typeof error.error
              ?.message === 'string'
              ? error.error.message
              : '';

          this.errorMessage.set(
            apiMessage ||
              'The account could not be created.',
          );
        },
      });
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(
      value =>
        !value,
    );
  }

  private getSafeReturnUrl(): string {
    const returnUrl =
      this.route.snapshot
        .queryParamMap
        .get(
          'returnUrl',
        ) ??
      '/admin/users';

    const isSafeLocalUrl =
      returnUrl.startsWith('/') &&
      !returnUrl.startsWith('//') &&
      !returnUrl.includes('://');

    return isSafeLocalUrl
      ? returnUrl
      : '/admin/users';
  }
}