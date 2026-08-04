import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
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
  FullCalendarComponent,
  FullCalendarModule,
} from '@fullcalendar/angular';
import dayGridPlugin
  from '@fullcalendar/angular/daygrid';
import interactionPlugin
  from '@fullcalendar/angular/interaction';
import listPlugin
  from '@fullcalendar/angular/list';
import themePlugin
  from '@fullcalendar/angular/themes/classic';

import {
  ProductionCalendarDashboard,
  ProductionCalendarEvent,
} from '../../core/models/production-api.model';
import {
  ProductionApiService,
} from '../../core/services/production-api.service';

interface FilterOption {
  readonly value: string;
  readonly label: string;
}

interface SavedCalendarFilters {
  readonly search: string;
  readonly eventTypes: readonly string[];
  readonly statuses: readonly string[];
  readonly handler: string;
  readonly department: string;
  readonly poNumber: string;
}

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

  private readonly calendar =
    viewChild(FullCalendarComponent);

  private readonly workspace =
    viewChild<ElementRef<HTMLElement>>(
      'calendarWorkspace',
    );

  private readonly savedFilterKey =
    'poTracker.productionCalendar.filters.v1';

  readonly errorMessage = signal('');

  readonly successMessage = signal('');

  readonly isLoading = signal(false);

  readonly dashboardLoading = signal(false);

  readonly dashboard =
    signal<ProductionCalendarDashboard | null>(
      null,
    );

  readonly loadedEvents =
    signal<ProductionCalendarEvent[]>([]);

  readonly selectedEvent =
    signal<ProductionCalendarEvent | null>(
      null,
    );

  readonly visibleStart = signal('');

  readonly visibleEnd = signal('');

  readonly calendarTitle = signal(
    'Production Calendar',
  );

  readonly activeView = signal(
    'dayGridMonth',
  );

  readonly lastRefreshed = signal('Not refreshed');

  readonly searchFilter = signal('');

  readonly selectedEventTypes = signal<
    readonly string[]
  >([
    'Process Start',
    'Dispatch',
    'Delivery',
    'Overdue',
    'Completed',
  ]);

  readonly selectedStatuses = signal<
    readonly string[]
  >([
    'Pending',
    'In Process',
    'On Hold',
    'Hold',
    'Completed',
    'Partial',
    'Ready',
    'Dispatched',
  ]);

  readonly handlerFilter = signal('');

  readonly departmentFilter = signal('');

  readonly poNumberFilter = signal('');

  readonly availableHandlers =
    signal<readonly string[]>([]);

  readonly availableDepartments =
    signal<readonly string[]>([]);

  readonly eventTypeOptions:
    readonly FilterOption[] = [
      {
        value: 'Process Start',
        label: 'Process Start',
      },
      {
        value: 'Dispatch',
        label: 'Dispatch',
      },
      {
        value: 'Delivery',
        label: 'Delivery',
      },
      {
        value: 'Overdue',
        label: 'Overdue',
      },
      {
        value: 'Completed',
        label: 'Completed',
      },
    ];

  readonly statusOptions:
    readonly FilterOption[] = [
      {
        value: 'Pending',
        label: 'Pending',
      },
      {
        value: 'In Process',
        label: 'In Process',
      },
      {
        value: 'On Hold',
        label: 'On Hold',
      },
      {
        value: 'Hold',
        label: 'Hold',
      },
      {
        value: 'Completed',
        label: 'Completed',
      },
      {
        value: 'Partial',
        label: 'Partially Dispatched',
      },
      {
        value: 'Ready',
        label: 'Ready',
      },
      {
        value: 'Dispatched',
        label: 'Dispatched',
      },
    ];

  readonly selectedEventTypeCount = computed(
    () => this.selectedEventTypes().length,
  );

  readonly selectedStatusCount = computed(
    () => this.selectedStatuses().length,
  );

  readonly totalDashboardEvents = computed(
    () => {
      const value = this.dashboard();

      if (!value) {
        return 0;
      }

      return (
        value.processStartEvents +
        value.dispatchEvents +
        value.deliveryEvents +
        value.overdueEvents +
        value.revisedEvents +
        value.completedEvents
      );
    },
  );

  readonly eventDonutBackground = computed(
    () => {
      const value = this.dashboard();
      const total = this.totalDashboardEvents();

      if (!value || total <= 0) {
        return '#eef2ff';
      }

      const start =
        (value.processStartEvents / total) * 100;
      const dispatch =
        start +
        (value.dispatchEvents / total) * 100;
      const delivery =
        dispatch +
        (value.deliveryEvents / total) * 100;
      const overdue =
        delivery +
        (value.overdueEvents / total) * 100;
      const revised =
        overdue +
        (value.revisedEvents / total) * 100;

      return [
        'conic-gradient(',
        `#2563eb 0 ${start}%,`,
        `#f97316 ${start}% ${dispatch}%,`,
        `#16a34a ${dispatch}% ${delivery}%,`,
        `#dc2626 ${delivery}% ${overdue}%,`,
        `#7c3aed ${overdue}% ${revised}%,`,
        `#64748b ${revised}% 100%`,
        ')',
      ].join(' ');
    },
  );

  readonly calendarOptions:
    CalendarOptions = {
      plugins: [
        themePlugin,
        dayGridPlugin,
        listPlugin,
        interactionPlugin,
      ],

      initialView: 'dayGridMonth',
      firstDay: 1,
      height: 'auto',
      contentHeight: 'auto',
      expandRows: false,
      fixedWeekCount: false,
      dayMaxEvents: 3,
      navLinks: true,
      editable: false,
      selectable: false,
      eventDisplay: 'block',
      nowIndicator: true,
      headerToolbar: false,

      views: {
        dayGridMonth: {
          dayMaxEvents: 3,
        },
        dayGridWeek: {
          dayMaxEvents: 6,
        },
        listWeek: {
          noEventsContent:
            'No production events found.',
        },
      },

      events: (
        fetchInfo,
        successCallback,
        failureCallback,
      ) => {
        this.visibleStart.set(
          fetchInfo.startStr,
        );
        this.visibleEnd.set(
          fetchInfo.endStr,
        );

        this.errorMessage.set('');
        this.isLoading.set(true);

        this.productionApi
          .getCalendarEvents({
            start: fetchInfo.startStr,
            end: fetchInfo.endStr,
            search: this.searchFilter(),
            eventTypes:
              this.selectedEventTypes(),
            statuses:
              this.selectedStatuses(),
            handler: this.handlerFilter(),
            department:
              this.departmentFilter(),
            poNumber:
              this.poNumberFilter(),
          })
          .pipe(
            takeUntilDestroyed(
              this.destroyRef,
            ),
          )
          .subscribe({
            next: (events) => {
              this.loadedEvents.set(events);
              this.updateFilterOptions(events);
              this.isLoading.set(false);
              this.updateRefreshTime();

              successCallback(
                events.map((event) => ({
                  id: event.id,
                  title: event.title,
                  start: event.start,
                  color: event.color,
                  allDay: event.allDay,
                  extendedProps: {
                    record: event,
                    materialId:
                      event.materialId,
                    purchaseOrderId:
                      event.purchaseOrderId,
                    poNumber:
                      event.poNumber,
                    materialNo:
                      event.materialNo,
                    eventType:
                      event.eventType,
                    status: event.status,
                  },
                })),
              );
            },

            error: (error) => {
              this.isLoading.set(false);
              this.errorMessage.set(
                error?.error?.message ??
                'Calendar events could not be loaded.',
              );

              failureCallback(error);
            },
          });
      },

      datesSet: (info) => {
        this.calendarTitle.set(
          info.view.title,
        );
        this.activeView.set(
          info.view.type,
        );
      },

      eventClick: (info) => {
        const record =
          info.event.extendedProps[
            'record'
          ] as
            | ProductionCalendarEvent
            | undefined;

        if (record) {
          this.selectedEvent.set(record);
        }
      },

      eventContent: (info) => {
        const eventType = String(
          info.event.extendedProps[
            'eventType'
          ] ?? '',
        );

        const poNumber = String(
          info.event.extendedProps[
            'poNumber'
          ] ?? '',
        );

        const materialNo = String(
          info.event.extendedProps[
            'materialNo'
          ] ?? '',
        );

        const wrapper =
          document.createElement('div');
        wrapper.className =
          'calendar-event-card';

        const typeLine =
          document.createElement('span');
        typeLine.className =
          'calendar-event-card__type';
        typeLine.textContent = eventType;

        const poLine =
          document.createElement('strong');
        poLine.textContent = poNumber
          ? `PO ${poNumber}`
          : materialNo || info.event.title;

        wrapper.append(typeLine, poLine);

        return {
          domNodes: [wrapper],
        };
      },

      eventDidMount: (info) => {
        const record =
          info.event.extendedProps[
            'record'
          ] as
            | ProductionCalendarEvent
            | undefined;

        if (!record) {
          return;
        }

        const tooltip = [
          record.eventType,
          record.poNumber
            ? `PO ${record.poNumber}`
            : '',
          record.materialNo,
          record.description,
          `Pending: ${record.pendingQty} ${record.unit}`,
        ]
          .filter(Boolean)
          .join(' | ');

        info.el.setAttribute(
          'title',
          tooltip,
        );
      },
    };

  constructor() {
    this.restoreSavedFilters();
    this.loadDashboard();
  }

  setSearch(event: Event): void {
    this.searchFilter.set(
      this.inputValue(event),
    );
  }

  setHandler(event: Event): void {
    this.handlerFilter.set(
      this.inputValue(event),
    );
  }

  setDepartment(event: Event): void {
    this.departmentFilter.set(
      this.inputValue(event),
    );
  }

  setPoNumber(event: Event): void {
    this.poNumberFilter.set(
      this.inputValue(event),
    );
  }

  toggleEventType(
    value: string,
    event: Event,
  ): void {
    this.selectedEventTypes.set(
      this.toggleValue(
        this.selectedEventTypes(),
        value,
        this.checkedValue(event),
      ),
    );
  }

  toggleStatus(
    value: string,
    event: Event,
  ): void {
    this.selectedStatuses.set(
      this.toggleValue(
        this.selectedStatuses(),
        value,
        this.checkedValue(event),
      ),
    );
  }

  selectAllEventTypes(): void {
    this.selectedEventTypes.set(
      this.eventTypeOptions.map(
        option => option.value,
      ),
    );
  }

  selectAllStatuses(): void {
    this.selectedStatuses.set(
      this.statusOptions.map(
        option => option.value,
      ),
    );
  }

  clearFilters(): void {
    this.searchFilter.set('');
    this.selectedEventTypes.set(
      this.eventTypeOptions.map(
        option => option.value,
      ),
    );
    this.selectedStatuses.set(
      this.statusOptions.map(
        option => option.value,
      ),
    );
    this.handlerFilter.set('');
    this.departmentFilter.set('');
    this.poNumberFilter.set('');
    this.successMessage.set('');
    this.applyFilters();
  }

  applyFilters(): void {
    this.selectedEvent.set(null);
    this.calendarApi()?.refetchEvents();
  }

  saveCurrentFilter(): void {
    const value: SavedCalendarFilters = {
      search: this.searchFilter(),
      eventTypes:
        this.selectedEventTypes(),
      statuses:
        this.selectedStatuses(),
      handler: this.handlerFilter(),
      department:
        this.departmentFilter(),
      poNumber: this.poNumberFilter(),
    };

    localStorage.setItem(
      this.savedFilterKey,
      JSON.stringify(value),
    );

    this.successMessage.set(
      'Current calendar filters saved.',
    );

    window.setTimeout(
      () => this.successMessage.set(''),
      2500,
    );
  }

  previous(): void {
    this.calendarApi()?.prev();
  }

  next(): void {
    this.calendarApi()?.next();
  }

  today(): void {
    this.calendarApi()?.today();
  }

  showMonth(): void {
    this.calendarApi()?.changeView(
      'dayGridMonth',
    );
  }

  showWeek(): void {
    this.calendarApi()?.changeView(
      'dayGridWeek',
    );
  }

  showList(): void {
    this.calendarApi()?.changeView(
      'listWeek',
    );
  }

  showOverdue(): void {
    this.selectedEventTypes.set([
      'Overdue',
    ]);
    this.calendarApi()?.today();
    this.applyFilters();
  }

  showDueToday(): void {
    this.selectedEventTypes.set([
      'Delivery',
      'Overdue',
    ]);
    this.calendarApi()?.today();
    this.applyFilters();
  }

  showNextSevenDays(): void {
    this.selectedEventTypes.set([
      'Delivery',
    ]);
    this.calendarApi()?.today();
    this.applyFilters();
  }

  showCompleted(): void {
    this.selectedEventTypes.set([
      'Completed',
    ]);
    this.calendarApi()?.today();
    this.applyFilters();
  }

  showAllMaterials(): void {
    this.clearFilters();
  }

  refresh(): void {
    this.loadDashboard();
    this.applyFilters();
  }

  toggleFullscreen(): void {
    const element =
      this.workspace()?.nativeElement;

    if (!element) {
      return;
    }

    if (document.fullscreenElement) {
      void document.exitFullscreen();
      return;
    }

    void element.requestFullscreen();
  }

  exportCsv(): void {
    const events = this.loadedEvents();

    if (events.length === 0) {
      this.errorMessage.set(
        'There are no visible calendar events to export.',
      );
      return;
    }

    const rows = [
      [
        'Event Type',
        'Date',
        'PO Number',
        'Material Number',
        'Description',
        'Status',
        'Ordered Qty',
        'Dispatched Qty',
        'Pending Qty',
        'Unit',
        'Handler',
        'Department',
      ],
      ...events.map(event => [
        event.eventType,
        event.start,
        event.poNumber,
        event.materialNo,
        event.description,
        event.status,
        String(event.orderedQty),
        String(event.dispatchQty),
        String(event.pendingQty),
        event.unit,
        event.handler,
        event.department,
      ]),
    ];

    const csv = rows
      .map(row =>
        row
          .map(value =>
            this.csvValue(value),
          )
          .join(','),
      )
      .join('\r\n');

    const blob = new Blob(
      [csv],
      {
        type: 'text/csv;charset=utf-8',
      },
    );

    const url = URL.createObjectURL(blob);
    const anchor =
      document.createElement('a');

    anchor.href = url;
    anchor.download =
      `production-calendar-${this.fileDate()}.csv`;
    anchor.click();

    URL.revokeObjectURL(url);
  }

  closeDetails(): void {
    this.selectedEvent.set(null);
  }

  viewMaterial(
    event: ProductionCalendarEvent,
  ): void {
    void this.router.navigate([
      '/materials',
      event.materialId,
    ]);
  }

  editMaterial(
    event: ProductionCalendarEvent,
  ): void {
    void this.router.navigate([
      '/materials',
      event.materialId,
      'edit',
    ]);
  }

  viewPurchaseOrder(
    event: ProductionCalendarEvent,
  ): void {
    void this.router.navigate([
      '/purchase-orders',
      event.purchaseOrderId,
    ]);
  }

  addDispatch(
    event: ProductionCalendarEvent,
  ): void {
    void this.router.navigate([
      '/dispatches',
      event.materialId,
    ]);
  }

  uploadDrawing(
    event: ProductionCalendarEvent,
  ): void {
    void this.router.navigate([
      '/materials',
      event.materialId,
      'files',
    ]);
  }

  openUpcomingMaterial(
    materialId: number,
  ): void {
    void this.router.navigate([
      '/materials',
      materialId,
    ]);
  }

  formatDate(
    value: string | null | undefined,
  ): string {
    if (!value) {
      return '-';
    }

    const normalized =
      value.length === 10
        ? `${value}T00:00:00`
        : value;

    const date = new Date(normalized);

    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return new Intl.DateTimeFormat(
      'en-GB',
      {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      },
    ).format(date);
  }

  eventTypeClass(
    eventType: string,
  ): string {
    const slug = eventType
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '');

    return `event-type event-type--${slug}`;
  }

  workloadClass(level: string): string {
    return (
      'workload-level workload-level--' +
      level.toLowerCase()
    );
  }

  private loadDashboard(): void {
    this.dashboardLoading.set(true);

    this.productionApi
      .getCalendarDashboard()
      .pipe(
        takeUntilDestroyed(
          this.destroyRef,
        ),
      )
      .subscribe({
        next: (dashboard) => {
          this.dashboard.set(dashboard);
          this.dashboardLoading.set(false);
          this.updateRefreshTime();
        },
        error: (error) => {
          this.dashboardLoading.set(false);
          this.errorMessage.set(
            error?.error?.message ??
            'Calendar summary could not be loaded.',
          );
        },
      });
  }

  private restoreSavedFilters(): void {
    const stored = localStorage.getItem(
      this.savedFilterKey,
    );

    if (!stored) {
      return;
    }

    try {
      const value = JSON.parse(
        stored,
      ) as Partial<SavedCalendarFilters>;

      this.searchFilter.set(
        value.search ?? '',
      );
      this.selectedEventTypes.set(
        value.eventTypes?.length
          ? value.eventTypes
          : this.selectedEventTypes(),
      );
      this.selectedStatuses.set(
        value.statuses?.length
          ? value.statuses
          : this.selectedStatuses(),
      );
      this.handlerFilter.set(
        value.handler ?? '',
      );
      this.departmentFilter.set(
        value.department ?? '',
      );
      this.poNumberFilter.set(
        value.poNumber ?? '',
      );
    } catch {
      localStorage.removeItem(
        this.savedFilterKey,
      );
    }
  }

  private updateFilterOptions(
    events: readonly ProductionCalendarEvent[],
  ): void {
    this.availableHandlers.update(
      current =>
        this.mergeOptions(
          current,
          events.map(event =>
            event.handler,
          ),
        ),
    );

    this.availableDepartments.update(
      current =>
        this.mergeOptions(
          current,
          events.map(event =>
            event.department,
          ),
        ),
    );
  }

  private mergeOptions(
    current: readonly string[],
    incoming: readonly string[],
  ): readonly string[] {
    return Array.from(
      new Set(
        [...current, ...incoming]
          .map(value => value.trim())
          .filter(Boolean),
      ),
    ).sort((left, right) =>
      left.localeCompare(right),
    );
  }

  private toggleValue(
    current: readonly string[],
    value: string,
    checked: boolean,
  ): readonly string[] {
    if (checked) {
      return current.includes(value)
        ? current
        : [...current, value];
    }

    return current.filter(
      item => item !== value,
    );
  }

  private checkedValue(event: Event): boolean {
    return (
      event.target as HTMLInputElement
    ).checked;
  }

  private inputValue(event: Event): string {
    return (
      event.target as
        | HTMLInputElement
        | HTMLSelectElement
    ).value;
  }

  private calendarApi() {
    return this.calendar()?.getApi();
  }

  private updateRefreshTime(): void {
    this.lastRefreshed.set(
      new Intl.DateTimeFormat(
        'en-GB',
        {
          day: '2-digit',
          month: 'short',
          year: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
        },
      ).format(new Date()),
    );
  }

  private csvValue(value: string): string {
    return (
      '"' +
      value.replaceAll('"', '""') +
      '"'
    );
  }

  private fileDate(): string {
    return new Date()
      .toISOString()
      .slice(0, 10);
  }
}