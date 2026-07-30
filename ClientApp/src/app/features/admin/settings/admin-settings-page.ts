import {
  ChangeDetectionStrategy,
  Component,
} from '@angular/core';

import {
  RouterLink,
} from '@angular/router';

@Component({
  selector: 'app-admin-settings-page',
  imports: [
    RouterLink,
  ],
  templateUrl: './admin-settings-page.html',
  styleUrl: '../admin-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class AdminSettingsPage {
  readonly processStatuses = [
    'Pending',
    'In Process',
    'Hold',
    'Completed',
  ] as const;

  readonly dispatchStatuses = [
    'Pending',
    'Partial',
    'Ready',
    'Dispatched',
  ] as const;

  readonly owners = [
    'Paramaguru',
    'Gokulnath',
    'Sam',
    'Abdul',
    'Kalai',
  ] as const;
}