import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import {
  takeUntilDestroyed,
} from '@angular/core/rxjs-interop';
import {
  Router,
  RouterLink,
} from '@angular/router';
import {
  CalendarOptions,
  FullCalendarModule,
} from '@fullcalendar/angular';
import dayGridPlugin
  from '@fullcalendar/angular/daygrid';
import interactionPlugin
  from '@fullcalendar/angular/interaction';
import themePlugin
  from '@fullcalendar/angular/themes/classic';

import {
  ProductionApiService,
} from '../../core/services/production-api.service';

@Component({
  selector: 'app-production-calendar-page',
  imports: [
    FullCalendarModule,
    RouterLink,
  ],
  templateUrl:
    './production-calendar-page.html',
  styleUrl:
    './production-calendar-page.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class ProductionCalendarPage {
  private readonly productionApi =
    inject(ProductionApiService);

  private readonly router =
    inject(Router);

  private readonly destroyRef =
    inject(DestroyRef);


  readonly errorMessage = signal('');

  readonly calendarOptions:
    CalendarOptions = {
      plugins: [
        themePlugin,
        dayGridPlugin,
        interactionPlugin,
      ],

      initialView: 'dayGridMonth',
      firstDay: 1,
      height: 750,
      expandRows: true,
      fixedWeekCount: false,

      headerToolbar: {
        left: 'prev,next today',
        center: 'title',
        right: 'dayGridMonth',
      },

      buttons: {
        today: {
          text: 'Today',
          display: 'text',
        },

        dayGridMonth: {
          text: 'Month',
          display: 'text',
        },
      },

      events: (
        fetchInfo,
        successCallback,
        failureCallback,
      ) => {
        this.errorMessage.set('');

        this.productionApi
          .getCalendarEvents(
            fetchInfo.startStr,
            fetchInfo.endStr,
          )
          .pipe(
            takeUntilDestroyed(
              this.destroyRef,
            ),
          )
          .subscribe({
            next: (events) => {
              successCallback(
                events.map((event) => ({
                  id: String(event.id),
                  title: event.title,
                  start: event.start,
                  color: event.color,
                  allDay: event.allDay,

                  extendedProps: {
                    materialId:
                      event.materialId,

                    eventType:
                      event.eventType,
                  },
                })),
              );
            },

            error: (error) => {
              this.errorMessage.set(
                error?.error?.message ??
                'Calendar events could not be loaded.',
              );

              failureCallback(error);
            },
          });
      },

      eventClick: (info) => {
        const materialId = Number(
          info.event.extendedProps[
            'materialId'
          ] ??
          info.event.id,
        );

        if (
          Number.isInteger(materialId) &&
          materialId > 0
        ) {
          void this.router.navigate([
            '/materials',
            materialId,
          ]);
        }
      },

      eventDidMount: (info) => {
        const eventType =
          info.event.extendedProps[
            'eventType'
          ] ?? '';

        info.el.setAttribute(
          'title',
          eventType
            ? `${info.event.title} - ${eventType}`
            : info.event.title,
        );
      },
    };
}

