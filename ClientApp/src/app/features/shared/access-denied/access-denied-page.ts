import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import {
  AuthApiService,
} from '../../../core/services/auth-api.service';

@Component({
  selector: 'app-access-denied-page',
  imports: [
    RouterLink,
  ],
  templateUrl: './access-denied-page.html',
  styleUrl: './access-denied-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessDeniedPage {
  private readonly authApi =
    inject(AuthApiService);

  readonly currentUser =
    this.authApi.currentUser;
}
