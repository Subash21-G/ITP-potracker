import {
  Directive,
  ElementRef,
  Renderer2,
  effect,
  inject,
} from '@angular/core';

import { AuthApiService } from '../services/auth-api.service';

/**
 * Hides write/manage actions from Viewer users.
 * Backend authorization and route guards remain the security boundary.
 */
@Directive({
  selector: '[appManageOnly]',
  standalone: true,
})
export class ManageOnlyDirective {
  private readonly authApi = inject(AuthApiService);
  private readonly element = inject(ElementRef<HTMLElement>);
  private readonly renderer = inject(Renderer2);

  constructor() {
    effect(() => {
      const canOperate = this.authApi.canOperate();

      this.renderer.setProperty(
        this.element.nativeElement,
        'hidden',
        !canOperate,
      );

      this.renderer.setAttribute(
        this.element.nativeElement,
        'aria-hidden',
        canOperate ? 'false' : 'true',
      );
    });
  }
}
