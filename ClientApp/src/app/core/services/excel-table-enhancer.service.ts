import {
  DOCUMENT,
} from '@angular/common';

import {
  inject,
  Injectable,
  OnDestroy,
} from '@angular/core';

interface TableFilterState {
  readonly valuesByColumn: Map<
    number,
    ReadonlySet<string>
  >;
}

@Injectable({
  providedIn: 'root',
})
export class ExcelTableEnhancerService
  implements OnDestroy
{
  private readonly document =
    inject(DOCUMENT);

  private observer:
    MutationObserver | null =
      null;

  private activeMenu:
    HTMLDivElement | null =
      null;

  private readonly states =
    new WeakMap<
      HTMLTableElement,
      TableFilterState
    >();

  private readonly documentClickHandler =
    (event: Event): void => {
      const target =
        event.target;

      if (
        !(target instanceof Node) ||
        !this.activeMenu
      ) {
        return;
      }

      if (
        this.activeMenu.contains(target) ||
        (
          target instanceof Element &&
          target.closest(
            '.po-excel-filter-button',
          )
        )
      ) {
        return;
      }

      this.closeMenu();
    };

  start(): void {
    if (this.observer) {
      return;
    }

    this.enhanceAllTables();

    this.observer =
      new MutationObserver(
        () =>
          this.enhanceAllTables(),
      );

    this.observer.observe(
      this.document.body,
      {
        childList: true,
        subtree: true,
      },
    );

    this.document.addEventListener(
      'click',
      this.documentClickHandler,
      true,
    );
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.observer = null;

    this.document.removeEventListener(
      'click',
      this.documentClickHandler,
      true,
    );

    this.closeMenu();
  }

  private enhanceAllTables(): void {
    this.document
      .querySelectorAll<
        HTMLTableElement
      >(
        '.app-main:not(.app-main--login) table',
      )
      .forEach(
        (table) =>
          this.enhanceTable(table),
      );
  }

  private enhanceTable(
    table: HTMLTableElement,
  ): void {
    table.classList.add(
      'po-excel-table',
    );

    const viewport =
      table.parentElement;

    viewport?.classList.add(
      'po-excel-table-viewport',
    );

    table
      .querySelectorAll<
        HTMLTableCellElement
      >(
        ':scope > thead > tr > th',
      )
      .forEach(
        (headerCell) =>
          headerCell.classList.add(
            'po-excel-sticky-header',
          ),
      );

    if (
      table.dataset[
        'excelFilterMode'
      ] !== 'local'
    ) {
      return;
    }

    this.ensureLocalFilterState(
      table,
    );

    this.addLocalFilterButtons(
      table,
    );

    this.applyLocalFilters(
      table,
    );
  }

  private ensureLocalFilterState(
    table: HTMLTableElement,
  ): TableFilterState {
    const existing =
      this.states.get(table);

    if (existing) {
      return existing;
    }

    const created: TableFilterState = {
      valuesByColumn:
        new Map<
          number,
          ReadonlySet<string>
        >(),
    };

    this.states.set(
      table,
      created,
    );

    return created;
  }

  private addLocalFilterButtons(
    table: HTMLTableElement,
  ): void {
    const headerCells =
      Array.from(
        table.querySelectorAll<
          HTMLTableCellElement
        >(
          ':scope > thead > tr > th',
        ),
      );

    headerCells.forEach(
      (
        headerCell,
        columnIndex,
      ) => {
        if (
          this.shouldSkipHeader(
            headerCell,
          ) ||
          headerCell.querySelector(
            '.column-filter',
          ) ||
          headerCell.querySelector(
            '.po-excel-filter-button',
          )
        ) {
          return;
        }

        headerCell.classList.add(
          'po-excel-filter-header',
        );

        const button =
          this.document.createElement(
            'button',
          );

        button.type =
          'button';

        button.className =
          'po-excel-filter-button';

        button.setAttribute(
          'aria-label',
          `Filter ${this.headerText(
            headerCell,
          )}`,
        );

        button.title =
          'Excel-style filter';

        button.textContent =
          '▾';

        button.addEventListener(
          'click',
          (event) => {
            event.preventDefault();
            event.stopPropagation();

            this.openFilterMenu(
              table,
              columnIndex,
              button,
            );
          },
        );

        headerCell.appendChild(
          button,
        );

        this.refreshButtonState(
          table,
          columnIndex,
          button,
        );
      },
    );
  }

  private shouldSkipHeader(
    headerCell: HTMLTableCellElement,
  ): boolean {
    if (
      headerCell.querySelector(
        'input[type="checkbox"]',
      )
    ) {
      return true;
    }

    const text =
      this.headerText(
        headerCell,
      ).toLowerCase();

    return /^(action|actions|manage|change role|edit|delete|view)$/
      .test(text);
  }

  private headerText(
    headerCell: HTMLTableCellElement,
  ): string {
    const clone =
      headerCell.cloneNode(
        true,
      ) as HTMLTableCellElement;

    clone
      .querySelectorAll(
        '.po-excel-filter-button, .column-filter',
      )
      .forEach(
        element =>
          element.remove(),
      );

    return (
      clone.textContent ??
      'column'
    )
      .replace(
        /\s+/g,
        ' ',
      )
      .trim() ||
      'column';
  }

  private cellValue(
    row: HTMLTableRowElement,
    columnIndex: number,
  ): string {
    const cell =
      row.cells.item(
        columnIndex,
      );

    return (
      cell?.textContent ??
      ''
    )
      .replace(
        /\s+/g,
        ' ',
      )
      .trim();
  }

  private openFilterMenu(
    table: HTMLTableElement,
    columnIndex: number,
    button: HTMLButtonElement,
  ): void {
    this.closeMenu();

    const rows =
      Array.from(
        table.tBodies,
      ).flatMap(
        body =>
          Array.from(
            body.rows,
          ),
      );

    const values =
      Array.from(
        new Set(
          rows.map(
            row =>
              this.cellValue(
                row,
                columnIndex,
              ),
          ),
        ),
      ).sort(
        (
          left,
          right,
        ) =>
          left.localeCompare(
            right,
            undefined,
            {
              numeric: true,
              sensitivity: 'base',
            },
          ),
      );

    const state =
      this.ensureLocalFilterState(
        table,
      );

    const selected =
      state.valuesByColumn.get(
        columnIndex,
      );

    const menu =
      this.document.createElement(
        'div',
      );

    menu.className =
      'po-excel-filter-menu';

    menu.setAttribute(
      'role',
      'dialog',
    );

    menu.setAttribute(
      'aria-label',
      `Filter ${button.getAttribute(
        'aria-label',
      ) ?? 'column'}`,
    );

    const search =
      this.document.createElement(
        'input',
      );

    search.type =
      'search';

    search.className =
      'po-excel-filter-search';

    search.placeholder =
      'Search values';

    const optionList =
      this.document.createElement(
        'div',
      );

    optionList.className =
      'po-excel-filter-values';

    values.forEach(
      (
        value,
        optionIndex,
      ) => {
        const label =
          this.document.createElement(
            'label',
          );

        label.className =
          'po-excel-filter-value';

        label.dataset[
          'filterSearchText'
        ] =
          value.toLowerCase();

        const checkbox =
          this.document.createElement(
            'input',
          );

        checkbox.type =
          'checkbox';

        checkbox.value =
          value;

        checkbox.checked =
          !selected ||
          selected.has(value);

        checkbox.id =
          (
            'po-excel-filter-' +
            columnIndex +
            '-' +
            optionIndex +
            '-' +
            Math.random()
              .toString(36)
              .slice(2)
          );

        const text =
          this.document.createElement(
            'span',
          );

        text.textContent =
          value || '(Blank)';

        label.append(
          checkbox,
          text,
        );

        optionList.appendChild(
          label,
        );
      },
    );

    search.addEventListener(
      'input',
      () => {
        const query =
          search.value
            .trim()
            .toLowerCase();

        optionList
          .querySelectorAll<
            HTMLElement
          >(
            '.po-excel-filter-value',
          )
          .forEach(
            option => {
              const optionText =
                option.dataset[
                  'filterSearchText'
                ] ?? '';

              option.hidden =
                Boolean(query) &&
                !optionText.includes(
                  query,
                );
            },
          );
      },
    );

    const toolbar =
      this.document.createElement(
        'div',
      );

    toolbar.className =
      'po-excel-filter-toolbar';

    const selectAll =
      this.document.createElement(
        'button',
      );

    selectAll.type =
      'button';

    selectAll.textContent =
      'Select all';

    selectAll.addEventListener(
      'click',
      () => {
        optionList
          .querySelectorAll<
            HTMLInputElement
          >(
            'input[type="checkbox"]',
          )
          .forEach(
            checkbox => {
              if (
                !checkbox
                  .closest<HTMLElement>(
                    '.po-excel-filter-value',
                  )
                  ?.hidden
              ) {
                checkbox.checked =
                  true;
              }
            },
          );
      },
    );

    const clear =
      this.document.createElement(
        'button',
      );

    clear.type =
      'button';

    clear.textContent =
      'Clear';

    clear.addEventListener(
      'click',
      () => {
        state.valuesByColumn.delete(
          columnIndex,
        );

        this.applyLocalFilters(
          table,
        );

        this.refreshButtonState(
          table,
          columnIndex,
          button,
        );

        this.closeMenu();
      },
    );

    toolbar.append(
      selectAll,
      clear,
    );

    const actions =
      this.document.createElement(
        'div',
      );

    actions.className =
      'po-excel-filter-actions';

    const cancel =
      this.document.createElement(
        'button',
      );

    cancel.type =
      'button';

    cancel.textContent =
      'Cancel';

    cancel.addEventListener(
      'click',
      () =>
        this.closeMenu(),
    );

    const apply =
      this.document.createElement(
        'button',
      );

    apply.type =
      'button';

    apply.className =
      'po-excel-filter-apply';

    apply.textContent =
      'Apply';

    apply.addEventListener(
      'click',
      () => {
        const checkedValues =
          Array.from(
            optionList.querySelectorAll<
              HTMLInputElement
            >(
              'input[type="checkbox"]:checked',
            ),
          ).map(
            checkbox =>
              checkbox.value,
          );

        if (
          checkedValues.length ===
          values.length
        ) {
          state.valuesByColumn.delete(
            columnIndex,
          );
        }
        else {
          state.valuesByColumn.set(
            columnIndex,
            new Set(
              checkedValues,
            ),
          );
        }

        this.applyLocalFilters(
          table,
        );

        this.refreshButtonState(
          table,
          columnIndex,
          button,
        );

        this.closeMenu();
      },
    );

    actions.append(
      cancel,
      apply,
    );

    menu.append(
      search,
      toolbar,
      optionList,
      actions,
    );

    this.document.body.appendChild(
      menu,
    );

    const rect =
      button.getBoundingClientRect();

    const menuWidth =
      290;

    const viewportWidth =
      this.document.documentElement
        .clientWidth;

    const left =
      Math.max(
        10,
        Math.min(
          rect.left,
          viewportWidth -
            menuWidth -
            10,
        ),
      );

    menu.style.left =
      `${left}px`;

    menu.style.top =
      `${Math.min(
        rect.bottom + 7,
        this.document.documentElement
          .clientHeight - 430,
      )}px`;

    this.activeMenu =
      menu;

    search.focus();
  }

  private applyLocalFilters(
    table: HTMLTableElement,
  ): void {
    const state =
      this.ensureLocalFilterState(
        table,
      );

    const filters =
      state.valuesByColumn;

    Array.from(
      table.tBodies,
    )
      .flatMap(
        body =>
          Array.from(
            body.rows,
          ),
      )
      .forEach(
        row => {
          const visible =
            Array.from(
              filters.entries(),
            ).every(
              (
                [
                  columnIndex,
                  allowedValues,
                ],
              ) =>
                allowedValues.has(
                  this.cellValue(
                    row,
                    columnIndex,
                  ),
                ),
            );

          row.classList.toggle(
            'po-excel-row-filtered-out',
            !visible,
          );
        },
      );

    table
      .querySelectorAll<
        HTMLButtonElement
      >(
        '.po-excel-filter-button',
      )
      .forEach(
        (
          filterButton,
          columnIndex,
        ) =>
          this.refreshButtonState(
            table,
            columnIndex,
            filterButton,
          ),
      );
  }

  private refreshButtonState(
    table: HTMLTableElement,
    columnIndex: number,
    button: HTMLButtonElement,
  ): void {
    const active =
      this.ensureLocalFilterState(
        table,
      ).valuesByColumn.has(
        columnIndex,
      );

    button.classList.toggle(
      'po-excel-filter-button--active',
      active,
    );

    button.setAttribute(
      'aria-pressed',
      active
        ? 'true'
        : 'false',
    );
  }

  private closeMenu(): void {
    this.activeMenu?.remove();
    this.activeMenu = null;
  }
}