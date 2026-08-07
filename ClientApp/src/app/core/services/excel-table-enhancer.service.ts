import {
  DOCUMENT,
} from '@angular/common';

import {
  inject,
  Injectable,
  OnDestroy,
} from '@angular/core';

type ExcelSortDirection =
  | 'asc'
  | 'desc';

type ExcelSortKind =
  | 'text'
  | 'number'
  | 'date';

interface TableFilterState {
  readonly valuesByColumn: Map<
    number,
    ReadonlySet<string>
  >;

  sortColumn:
    number | null;

  sortDirection:
    ExcelSortDirection | null;

  readonly originalOrderByRow:
    WeakMap<
      HTMLTableRowElement,
      number
    >;

  nextOriginalOrder:
    number;
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
        table =>
          this.enhanceTable(
            table,
          ),
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
        headerCell =>
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

      sortColumn:
        null,

      sortDirection:
        null,

      originalOrderByRow:
        new WeakMap<
          HTMLTableRowElement,
          number
        >(),

      nextOriginalOrder:
        0,
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

        button.dataset[
          'excelColumnIndex'
        ] =
          String(
            columnIndex,
          );

        button.setAttribute(
          'aria-label',
          `Filter and sort ${this.headerText(
            headerCell,
          )}`,
        );

        button.title =
          'Excel-style filter and sort';

        button.textContent =
          'â–¾';

        button.addEventListener(
          'click',
          event => {
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

  private tableRows(
    table: HTMLTableElement,
  ): HTMLTableRowElement[] {
    return Array.from(
      table.tBodies,
    ).flatMap(
      body =>
        Array.from(
          body.rows,
        ),
    );
  }

  private ensureOriginalOrder(
    table: HTMLTableElement,
    state: TableFilterState,
  ): void {
    this.tableRows(
      table,
    ).forEach(
      row => {
        if (
          state
            .originalOrderByRow
            .has(row)
        ) {
          return;
        }

        state
          .originalOrderByRow
          .set(
            row,
            state.nextOriginalOrder,
          );

        state.nextOriginalOrder +=
          1;
      },
    );
  }

  private rowsAvailableForColumn(
    table: HTMLTableElement,
    columnIndex: number,
  ): HTMLTableRowElement[] {
    const state =
      this.ensureLocalFilterState(
        table,
      );

    return this.tableRows(
      table,
    ).filter(
      row =>
        Array.from(
          state
            .valuesByColumn
            .entries(),
        ).every(
          (
            [
              filterColumn,
              allowedValues,
            ],
          ) =>
            filterColumn ===
              columnIndex ||
            allowedValues.has(
              this.cellValue(
                row,
                filterColumn,
              ),
            ),
        ),
    );
  }

  private openFilterMenu(
    table: HTMLTableElement,
    columnIndex: number,
    button: HTMLButtonElement,
  ): void {
    this.closeMenu();

    const rows =
      this.rowsAvailableForColumn(
        table,
        columnIndex,
      );

    const rawValues =
      rows.map(
        row =>
          this.cellValue(
            row,
            columnIndex,
          ),
      );

    const sortKind =
      this.detectSortKind(
        table,
        columnIndex,
        rawValues,
      );

    const values =
      Array.from(
        new Set(
          rawValues,
        ),
      ).sort(
        (
          left,
          right,
        ) =>
          this.compareRawValues(
            left,
            right,
            sortKind,
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

    const headerCell =
      button.closest<
        HTMLTableCellElement
      >(
        'th',
      );

    const headerName =
      headerCell
        ? this.headerText(
            headerCell,
          )
        : 'column';

    menu.setAttribute(
      'aria-label',
      `Filter and sort ${headerName}`,
    );

    const sortSection =
      this.document.createElement(
        'div',
      );

    sortSection.className =
      'po-excel-filter-sort';

    const sortLabels =
      this.sortLabels(
        sortKind,
      );

    const ascending =
      this.document.createElement(
        'button',
      );

    ascending.type =
      'button';

    ascending.className =
      'po-excel-filter-sort-button po-excel-filter-sort-asc';

    ascending.textContent =
      `â†‘ ${sortLabels.ascending}`;

    ascending.classList.toggle(
      'po-excel-filter-sort-button--active',
      state.sortColumn ===
        columnIndex &&
        state.sortDirection ===
          'asc',
    );

    ascending.addEventListener(
      'click',
      () => {
        state.sortColumn =
          columnIndex;

        state.sortDirection =
          'asc';

        this.applyLocalFilters(
          table,
        );

        this.closeMenu();
      },
    );

    const descending =
      this.document.createElement(
        'button',
      );

    descending.type =
      'button';

    descending.className =
      'po-excel-filter-sort-button po-excel-filter-sort-desc';

    descending.textContent =
      `â†“ ${sortLabels.descending}`;

    descending.classList.toggle(
      'po-excel-filter-sort-button--active',
      state.sortColumn ===
        columnIndex &&
        state.sortDirection ===
          'desc',
    );

    descending.addEventListener(
      'click',
      () => {
        state.sortColumn =
          columnIndex;

        state.sortDirection =
          'desc';

        this.applyLocalFilters(
          table,
        );

        this.closeMenu();
      },
    );

    const clearSort =
      this.document.createElement(
        'button',
      );

    clearSort.type =
      'button';

    clearSort.className =
      'po-excel-filter-sort-button po-excel-filter-clear-sort';

    clearSort.textContent =
      'Clear sort';

    clearSort.disabled =
      state.sortColumn !==
        columnIndex;

    clearSort.addEventListener(
      'click',
      () => {
        if (
          state.sortColumn ===
          columnIndex
        ) {
          state.sortColumn =
            null;

          state.sortDirection =
            null;

          this.applyLocalFilters(
            table,
          );
        }

        this.closeMenu();
      },
    );

    sortSection.append(
      ascending,
      descending,
      clearSort,
    );

    const divider =
      this.document.createElement(
        'div',
      );

    divider.className =
      'po-excel-filter-divider';

    const contentsHeading =
      this.document.createElement(
        'div',
      );

    contentsHeading.className =
      'po-excel-filter-contents-heading';

    contentsHeading.textContent =
      `Filter by contents (${values.length})`;

    const search =
      this.document.createElement(
        'input',
      );

    search.type =
      'search';

    search.className =
      'po-excel-filter-search';

    search.placeholder =
      'Search contents';

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
          value ||
          '(Blank)';

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
                ] ??
                '';

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
              const option =
                checkbox.closest<
                  HTMLElement
                >(
                  '.po-excel-filter-value',
                );

              if (
                !option?.hidden
              ) {
                checkbox.checked =
                  true;
              }
            },
          );
      },
    );

    const selectNone =
      this.document.createElement(
        'button',
      );

    selectNone.type =
      'button';

    selectNone.textContent =
      'Select none';

    selectNone.addEventListener(
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
              const option =
                checkbox.closest<
                  HTMLElement
                >(
                  '.po-excel-filter-value',
                );

              if (
                !option?.hidden
              ) {
                checkbox.checked =
                  false;
              }
            },
          );
      },
    );

    toolbar.append(
      selectAll,
      selectNone,
    );

    const actions =
      this.document.createElement(
        'div',
      );

    actions.className =
      'po-excel-filter-actions';

    const clearFilter =
      this.document.createElement(
        'button',
      );

    clearFilter.type =
      'button';

    clearFilter.className =
      'po-excel-filter-clear';

    clearFilter.textContent =
      'Clear filter';

    clearFilter.disabled =
      !state.valuesByColumn.has(
        columnIndex,
      );

    clearFilter.addEventListener(
      'click',
      () => {
        state.valuesByColumn.delete(
          columnIndex,
        );

        this.applyLocalFilters(
          table,
        );

        this.closeMenu();
      },
    );

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

        this.closeMenu();
      },
    );

    actions.append(
      clearFilter,
      cancel,
      apply,
    );

    menu.append(
      sortSection,
      divider,
      contentsHeading,
      search,
      toolbar,
      optionList,
      actions,
    );

    this.document.body.appendChild(
      menu,
    );

    this.positionMenu(
      menu,
      button,
    );

    this.activeMenu =
      menu;

    search.focus();
  }

  private positionMenu(
    menu: HTMLDivElement,
    button: HTMLButtonElement,
  ): void {
    const rect =
      button.getBoundingClientRect();

    const menuWidth =
      320;

    const menuHeight =
      500;

    const viewportWidth =
      this.document
        .documentElement
        .clientWidth;

    const viewportHeight =
      this.document
        .documentElement
        .clientHeight;

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

    const preferredTop =
      rect.bottom +
      7;

    const maximumTop =
      Math.max(
        10,
        viewportHeight -
          menuHeight -
          10,
      );

    const top =
      Math.max(
        10,
        Math.min(
          preferredTop,
          maximumTop,
        ),
      );

    menu.style.left =
      `${left}px`;

    menu.style.top =
      `${top}px`;
  }

  private detectSortKind(
    table: HTMLTableElement,
    columnIndex: number,
    values: readonly string[],
  ): ExcelSortKind {
    const headerCell =
      table
        .querySelectorAll<
          HTMLTableCellElement
        >(
          ':scope > thead > tr > th',
        )
        .item(
          columnIndex,
        );

    const header =
      headerCell
        ? this.headerText(
            headerCell,
          ).toLowerCase()
        : '';

    if (
      /(date|updated|created|delivery|received|due|start)/i
        .test(header)
    ) {
      return 'date';
    }

    if (
      /(qty|quantity|price|amount|value|stock|balance|count|total|rate|percent|progress)/i
        .test(header)
    ) {
      return 'number';
    }

    const nonBlank =
      values.filter(
        value =>
          Boolean(
            value.trim(),
          ),
      );

    if (
      nonBlank.length ===
      0
    ) {
      return 'text';
    }

    const numericCount =
      nonBlank.filter(
        value =>
          this.numericValue(
            value,
          ) !==
          null,
      ).length;

    if (
      numericCount /
        nonBlank.length >=
      0.8
    ) {
      return 'number';
    }

    const dateCount =
      nonBlank.filter(
        value =>
          this.dateValue(
            value,
          ) !==
          null,
      ).length;

    if (
      dateCount /
        nonBlank.length >=
      0.8
    ) {
      return 'date';
    }

    return 'text';
  }

  private sortLabels(
    kind: ExcelSortKind,
  ): {
    readonly ascending: string;
    readonly descending: string;
  } {
    if (
      kind ===
      'number'
    ) {
      return {
        ascending:
          'Sort Smallest to Largest',

        descending:
          'Sort Largest to Smallest',
      };
    }

    if (
      kind ===
      'date'
    ) {
      return {
        ascending:
          'Sort Oldest to Newest',

        descending:
          'Sort Newest to Oldest',
      };
    }

    return {
      ascending:
        'Sort A to Z',

      descending:
        'Sort Z to A',
    };
  }

  private numericValue(
    value: string,
  ): number | null {
    const normalized =
      value
        .replace(
          /[â‚¹$â‚¬Â£,%]/g,
          '',
        )
        .replace(
          /,/g,
          '',
        )
        .replace(
          /\s+/g,
          '',
        );

    if (
      !normalized ||
      !/^[+-]?(?:\d+\.?\d*|\.\d+)$/
        .test(
          normalized,
        )
    ) {
      return null;
    }

    const parsed =
      Number(
        normalized,
      );

    return Number.isFinite(
      parsed,
    )
      ? parsed
      : null;
  }

  private dateValue(
    value: string,
  ): number | null {
    const normalized =
      value.trim();

    if (!normalized) {
      return null;
    }

    const dayFirst =
      /^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/
        .exec(
          normalized,
        );

    if (dayFirst) {
      const day =
        Number(
          dayFirst[1],
        );

      const month =
        Number(
          dayFirst[2],
        );

      const year =
        Number(
          dayFirst[3],
        );

      const date =
        new Date(
          year,
          month - 1,
          day,
        );

      if (
        date.getFullYear() ===
          year &&
        date.getMonth() ===
          month - 1 &&
        date.getDate() ===
          day
      ) {
        return date.getTime();
      }
    }

    const parsed =
      Date.parse(
        normalized,
      );

    return Number.isNaN(
      parsed,
    )
      ? null
      : parsed;
  }

  private compareRawValues(
    left: string,
    right: string,
    kind: ExcelSortKind,
  ): number {
    const leftBlank =
      !left.trim();

    const rightBlank =
      !right.trim();

    if (
      leftBlank ||
      rightBlank
    ) {
      if (
        leftBlank &&
        rightBlank
      ) {
        return 0;
      }

      return leftBlank
        ? 1
        : -1;
    }

    if (
      kind ===
      'number'
    ) {
      const leftNumber =
        this.numericValue(
          left,
        );

      const rightNumber =
        this.numericValue(
          right,
        );

      if (
        leftNumber !==
          null &&
        rightNumber !==
          null
      ) {
        return (
          leftNumber -
          rightNumber
        );
      }
    }

    if (
      kind ===
      'date'
    ) {
      const leftDate =
        this.dateValue(
          left,
        );

      const rightDate =
        this.dateValue(
          right,
        );

      if (
        leftDate !==
          null &&
        rightDate !==
          null
      ) {
        return (
          leftDate -
          rightDate
        );
      }
    }

    return left.localeCompare(
      right,
      undefined,
      {
        numeric: true,
        sensitivity: 'base',
      },
    );
  }

  private compareRows(
    left: HTMLTableRowElement,
    right: HTMLTableRowElement,
    columnIndex: number,
    direction: ExcelSortDirection,
    kind: ExcelSortKind,
    state: TableFilterState,
  ): number {
    const leftValue =
      this.cellValue(
        left,
        columnIndex,
      );

    const rightValue =
      this.cellValue(
        right,
        columnIndex,
      );

    const leftBlank =
      !leftValue.trim();

    const rightBlank =
      !rightValue.trim();

    if (
      leftBlank !==
      rightBlank
    ) {
      return leftBlank
        ? 1
        : -1;
    }

    let comparison =
      this.compareRawValues(
        leftValue,
        rightValue,
        kind,
      );

    if (
      direction ===
      'desc'
    ) {
      comparison *=
        -1;
    }

    if (
      comparison !==
      0
    ) {
      return comparison;
    }

    const leftOrder =
      state
        .originalOrderByRow
        .get(left) ??
      0;

    const rightOrder =
      state
        .originalOrderByRow
        .get(right) ??
      0;

    return (
      leftOrder -
      rightOrder
    );
  }

  private applySort(
    table: HTMLTableElement,
    state: TableFilterState,
  ): void {
    this.ensureOriginalOrder(
      table,
      state,
    );

    Array.from(
      table.tBodies,
    ).forEach(
      body => {
        const currentRows =
          Array.from(
            body.rows,
          );

        let desiredRows =
          [...currentRows];

        if (
          state.sortColumn !==
            null &&
          state.sortDirection !==
            null
        ) {
          const values =
            currentRows.map(
              row =>
                this.cellValue(
                  row,
                  state.sortColumn ??
                    0,
                ),
            );

          const kind =
            this.detectSortKind(
              table,
              state.sortColumn,
              values,
            );

          desiredRows =
            desiredRows.sort(
              (
                left,
                right,
              ) =>
                this.compareRows(
                  left,
                  right,
                  state.sortColumn ??
                    0,
                  state.sortDirection ??
                    'asc',
                  kind,
                  state,
                ),
            );
        }
        else {
          desiredRows =
            desiredRows.sort(
              (
                left,
                right,
              ) => {
                const leftOrder =
                  state
                    .originalOrderByRow
                    .get(left) ??
                  0;

                const rightOrder =
                  state
                    .originalOrderByRow
                    .get(right) ??
                  0;

                return (
                  leftOrder -
                  rightOrder
                );
              },
            );
        }

        const changed =
          desiredRows.some(
            (
              row,
              index,
            ) =>
              row !==
              currentRows[index],
          );

        if (changed) {
          body.append(
            ...desiredRows,
          );
        }
      },
    );
  }

  private applyLocalFilters(
    table: HTMLTableElement,
  ): void {
    const state =
      this.ensureLocalFilterState(
        table,
      );

    this.ensureOriginalOrder(
      table,
      state,
    );

    const filters =
      state.valuesByColumn;

    this.tableRows(
      table,
    ).forEach(
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

    this.applySort(
      table,
      state,
    );

    const headerCells =
      Array.from(
        table.querySelectorAll<
          HTMLTableCellElement
        >(
          ':scope > thead > tr > th',
        ),
      );

    table
      .querySelectorAll<
        HTMLButtonElement
      >(
        '.po-excel-filter-button',
      )
      .forEach(
        filterButton => {
          const headerCell =
            filterButton.closest<
              HTMLTableCellElement
            >(
              'th',
            );

          if (!headerCell) {
            return;
          }

          const columnIndex =
            headerCells.indexOf(
              headerCell,
            );

          if (
            columnIndex <
            0
          ) {
            return;
          }

          this.refreshButtonState(
            table,
            columnIndex,
            filterButton,
          );
        },
      );
  }

  private refreshButtonState(
    table: HTMLTableElement,
    columnIndex: number,
    button: HTMLButtonElement,
  ): void {
    const state =
      this.ensureLocalFilterState(
        table,
      );

    const filtered =
      state.valuesByColumn.has(
        columnIndex,
      );

    const sorted =
      state.sortColumn ===
        columnIndex &&
      state.sortDirection !==
        null;

    const ascending =
      sorted &&
      state.sortDirection ===
        'asc';

    const descending =
      sorted &&
      state.sortDirection ===
        'desc';

    button.classList.toggle(
      'po-excel-filter-button--active',
      filtered,
    );

    button.classList.toggle(
      'po-excel-filter-button--sorted',
      sorted,
    );

    button.classList.toggle(
      'po-excel-filter-button--sorted-asc',
      ascending,
    );

    button.classList.toggle(
      'po-excel-filter-button--sorted-desc',
      descending,
    );

    const nextButtonText =
      ascending
        ? '\u2191'
        : descending
          ? '\u2193'
          : '\u25BE';

    if (
      button.textContent !==
      nextButtonText
    ) {
      button.textContent =
        nextButtonText;
    }
button.setAttribute(
      'aria-pressed',
      filtered ||
        sorted
        ? 'true'
        : 'false',
    );

    const states:
      string[] =
        [];

    if (ascending) {
      states.push(
        'sorted ascending',
      );
    }

    if (descending) {
      states.push(
        'sorted descending',
      );
    }

    if (filtered) {
      states.push(
        'filter active',
      );
    }

    button.title =
      states.length >
      0
        ? states.join(
            ' Â· ',
          )
        : 'Excel-style filter and sort';
  }

  private closeMenu(): void {
    this.activeMenu?.remove();
    this.activeMenu = null;
  }
}
