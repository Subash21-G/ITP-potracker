import {
  DOCUMENT,
} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  ViewChild,
  inject,
} from '@angular/core';

export type ServerExcelSortDirection =
  | 'asc'
  | 'desc'
  | null;

export interface ServerExcelFilterOption {
  readonly value: string;
  readonly label: string;
  readonly count?: number;
  readonly selected?: boolean;
}

@Component({
  selector: 'app-server-excel-filter',
  standalone: true,
  templateUrl: './server-excel-filter.html',
  styleUrl: './server-excel-filter.css',
  changeDetection:
    ChangeDetectionStrategy.OnPush,
})
export class ServerExcelFilter {
  private readonly document =
    inject(DOCUMENT);

  @ViewChild(
    'trigger',
    {
      read: ElementRef,
    },
  )
  private triggerElement?:
    ElementRef<HTMLButtonElement>;

  @ViewChild(
    'menu',
    {
      read: ElementRef,
    },
  )
  private menuElement?:
    ElementRef<HTMLDivElement>;

  @ViewChild(
    'searchInput',
    {
      read: ElementRef,
    },
  )
  private searchElement?:
    ElementRef<HTMLInputElement>;

  @Input({ required: true })
  title = '';

  @Input()
  options:
    readonly ServerExcelFilterOption[] =
      [];

  @Input()
  loading = false;

  @Input()
  active = false;

  @Input()
  selectionActive = false;

  @Input()
  selectedValues:
    readonly string[] =
      [];

  @Input()
  sortDirection:
    ServerExcelSortDirection =
      null;

  @Output()
  readonly opened =
    new EventEmitter<void>();

  @Output()
  readonly sortRequested =
    new EventEmitter<
      Exclude<
        ServerExcelSortDirection,
        null
      >
    >();

  @Output()
  readonly applyValues =
    new EventEmitter<
      readonly string[]
    >();

  @Output()
  readonly clearRequested =
    new EventEmitter<void>();

  isOpen = false;
  searchText = '';

  private draftSelected =
    new Set<string>();

  private draftSelectionActive =
    false;

  private positionFrame:
    number | null =
      null;

  get filteredOptions():
    readonly ServerExcelFilterOption[] {
    const search =
      this.searchText
        .trim()
        .toLocaleLowerCase();

    const values =
      search
        ? this.options.filter(
            option =>
              option.label
                .toLocaleLowerCase()
                .includes(
                  search,
                ),
          )
        : [...this.options];

    return values.sort(
      (
        first,
        second,
      ) =>
        first.label.localeCompare(
          second.label,
          undefined,
          {
            numeric: true,
            sensitivity: 'base',
          },
        ),
    );
  }

  get selectedCount(): number {
    return this.draftSelectionActive
      ? this.draftSelected.size
      : this.options.length;
  }

  get allVisibleSelected(): boolean {
    const visible =
      this.filteredOptions;

    return (
      visible.length > 0 &&
      (
        !this.draftSelectionActive ||
        visible.every(
          option =>
            this.draftSelected.has(
              option.value,
            ),
        )
      )
    );
  }

  toggleMenu(
    event: MouseEvent,
  ): void {
    event.stopPropagation();

    if (this.isOpen) {
      this.closeMenu();
      return;
    }

    this.openMenu();
  }

  onMenuToggle(
    event: Event,
  ): void {
    const menu =
      event.currentTarget as
        HTMLDivElement & {
          matches:
            (
              selectors: string,
            ) => boolean;
        };

    const open =
      menu.matches(
        ':popover-open',
      );

    if (open) {
      this.isOpen = true;
      this.startViewportTracking();
      this.scheduleMenuPosition();
      return;
    }

    this.isOpen = false;
    this.stopViewportTracking();
    this.cancelPositionFrame();
  }

  requestSort(
    direction:
      Exclude<
        ServerExcelSortDirection,
        null
      >,
  ): void {
    if (this.loading) {
      return;
    }

    this.sortRequested.emit(
      direction,
    );

    this.closeMenu();
  }

  onSearchInput(
    event: Event,
  ): void {
    this.searchText =
      (
        event.target as
          HTMLInputElement
      ).value;
  }

  isSelected(
    value: string,
  ): boolean {
    return (
      !this.draftSelectionActive ||
      this.draftSelected.has(
        value,
      )
    );
  }

  toggleValue(
    value: string,
    event: Event,
  ): void {
    const checked =
      (
        event.target as
          HTMLInputElement
      ).checked;

    this.ensureExplicitDraft();

    if (checked) {
      this.draftSelected.add(
        value,
      );
      return;
    }

    this.draftSelected.delete(
      value,
    );
  }

  selectAllVisible(): void {
    if (
      !this.draftSelectionActive
    ) {
      return;
    }

    for (
      const option of
      this.filteredOptions
    ) {
      this.draftSelected.add(
        option.value,
      );
    }
  }

  selectNoneVisible(): void {
    this.ensureExplicitDraft();

    for (
      const option of
      this.filteredOptions
    ) {
      this.draftSelected.delete(
        option.value,
      );
    }
  }

  clearFilter(): void {
    if (this.loading) {
      return;
    }

    this.draftSelected.clear();
    this.draftSelectionActive =
      false;
    this.clearRequested.emit();
    this.closeMenu();
  }

  cancel(): void {
    this.closeMenu();
  }

  apply(): void {
    if (this.loading) {
      return;
    }

    this.applyValues.emit(
      this.draftSelectionActive
        ? [...this.draftSelected]
        : this.options.map(
            option =>
              option.value,
          ),
    );

    this.closeMenu();
  }

  @HostListener(
    'window:resize',
  )
  onWindowResize(): void {
    this.scheduleMenuPosition();
  }

  private openMenu(): void {
    const menu =
      this.menuElement
        ?.nativeElement as
        (
          HTMLDivElement & {
            showPopover?:
              () => void;
          }
        ) |
        undefined;

    if (!menu) {
      return;
    }

    this.searchText = '';

    this.draftSelectionActive =
      this.selectionActive;

    this.draftSelected =
      new Set(
        this.selectedValues,
      );

    this.opened.emit();

    try {
      menu.showPopover?.();
    }
    catch {
      menu.style.display =
        'block';

      this.isOpen = true;
    }

    this.startViewportTracking();
    this.scheduleMenuPosition();

    this.document
      .defaultView
      ?.requestAnimationFrame(
        () => {
          this.searchElement
            ?.nativeElement
            .focus();
        },
      );
  }

  private ensureExplicitDraft():
    void {
    if (
      this.draftSelectionActive
    ) {
      return;
    }

    this.draftSelectionActive =
      true;

    this.draftSelected =
      new Set(
        this.options.map(
          option =>
            option.value,
        ),
      );
  }

  private closeMenu(): void {
    const menu =
      this.menuElement
        ?.nativeElement as
        (
          HTMLDivElement & {
            hidePopover?:
              () => void;
          }
        ) |
        undefined;

    if (menu) {
      try {
        menu.hidePopover?.();
      }
      catch {
        menu.style.display =
          'none';
      }
    }

    this.isOpen = false;
    this.stopViewportTracking();
    this.cancelPositionFrame();
  }

  private readonly viewportHandler =
    (): void => {
      this.scheduleMenuPosition();
    };

  private startViewportTracking():
    void {
    this.document.addEventListener(
      'scroll',
      this.viewportHandler,
      true,
    );

    this.document
      .defaultView
      ?.addEventListener(
        'resize',
        this.viewportHandler,
      );
  }

  private stopViewportTracking():
    void {
    this.document.removeEventListener(
      'scroll',
      this.viewportHandler,
      true,
    );

    this.document
      .defaultView
      ?.removeEventListener(
        'resize',
        this.viewportHandler,
      );
  }

  private scheduleMenuPosition():
    void {
    if (!this.isOpen) {
      return;
    }

    const ownerWindow =
      this.document.defaultView;

    if (
      !ownerWindow ||
      typeof ownerWindow
        .requestAnimationFrame !==
        'function'
    ) {
      this.positionMenu();
      return;
    }

    if (
      this.positionFrame !==
      null
    ) {
      return;
    }

    this.positionFrame =
      ownerWindow
        .requestAnimationFrame(
          () => {
            this.positionFrame =
              null;

            this.positionMenu();
          },
        );
  }

  private cancelPositionFrame():
    void {
    if (
      this.positionFrame ===
      null
    ) {
      return;
    }

    this.document
      .defaultView
      ?.cancelAnimationFrame(
        this.positionFrame,
      );

    this.positionFrame =
      null;
  }

  private positionMenu(): void {
    if (!this.isOpen) {
      return;
    }

    const trigger =
      this.triggerElement
        ?.nativeElement;

    const menu =
      this.menuElement
        ?.nativeElement;

    if (
      !trigger ||
      !menu
    ) {
      return;
    }

    const triggerRect =
      trigger.getBoundingClientRect();

    const documentElement =
      this.document
        .documentElement;

    const ownerWindow =
      this.document
        .defaultView;

    const viewportWidth =
      ownerWindow?.innerWidth ??
      documentElement.clientWidth;

    const viewportHeight =
      ownerWindow?.innerHeight ??
      documentElement.clientHeight;

    const margin = 12;
    const gap = 8;
    const preferredMaximumHeight =
      520;

    menu.style.maxHeight = '';

    const initialRect =
      menu.getBoundingClientRect();

    const menuWidth =
      initialRect.width > 0
        ? initialRect.width
        : 310;

    const maximumLeft =
      Math.max(
        margin,
        viewportWidth -
          margin -
          menuWidth,
      );

    let left =
      triggerRect.left;

    if (
      left + menuWidth >
      viewportWidth - margin
    ) {
      left =
        triggerRect.right -
        menuWidth;
    }

    left =
      Math.min(
        Math.max(
          margin,
          left,
        ),
        maximumLeft,
      );

    const naturalHeight =
      Math.min(
        Math.max(
          menu.scrollHeight,
          initialRect.height,
        ),
        preferredMaximumHeight,
      );

    const spaceBelow =
      Math.max(
        0,
        viewportHeight -
          triggerRect.bottom -
          gap -
          margin,
      );

    const spaceAbove =
      Math.max(
        0,
        triggerRect.top -
          gap -
          margin,
      );

    const openBelow =
      spaceBelow >= naturalHeight ||
      spaceBelow >= spaceAbove;

    const availableSpace =
      Math.max(
        1,
        openBelow
          ? spaceBelow
          : spaceAbove,
      );

    const maxHeight =
      Math.min(
        preferredMaximumHeight,
        availableSpace,
      );

    menu.style.maxHeight =
      String(
        Math.floor(
          maxHeight,
        ),
      ) + 'px';

    const renderedHeight =
      Math.min(
        menu
          .getBoundingClientRect()
          .height,
        maxHeight,
      );

    let top =
      openBelow
        ? triggerRect.bottom + gap
        : triggerRect.top -
          gap -
          renderedHeight;

    const maximumTop =
      Math.max(
        margin,
        viewportHeight -
          margin -
          renderedHeight,
      );

    top =
      Math.min(
        Math.max(
          margin,
          top,
        ),
        maximumTop,
      );

    menu.style.left =
      String(
        Math.round(
          left,
        ),
      ) + 'px';

    menu.style.top =
      String(
        Math.round(
          top,
        ),
      ) + 'px';
  }
}
