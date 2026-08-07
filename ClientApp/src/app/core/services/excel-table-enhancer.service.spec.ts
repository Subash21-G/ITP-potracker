import {
  TestBed,
} from '@angular/core/testing';

import {
  ExcelTableEnhancerService,
} from './excel-table-enhancer.service';

describe(
  'ExcelTableEnhancerService',
  () => {
    let service:
      ExcelTableEnhancerService;

    const buttonFor =
      (
        headerText: string,
      ):
        HTMLButtonElement | null =>
        Array.from(
          document.querySelectorAll<
            HTMLButtonElement
          >(
            '.po-excel-filter-button',
          ),
        ).find(
          button =>
            button
              .closest('th')
              ?.textContent
              ?.includes(
                headerText,
              ),
        ) ??
        null;

    const rowValues =
      (
        columnIndex: number,
      ): string[] =>
        Array.from(
          document.querySelectorAll<
            HTMLTableRowElement
          >(
            'tbody tr',
          ),
        ).map(
          row =>
            row.cells
              .item(
                columnIndex,
              )
              ?.textContent
              ?.trim() ??
            '',
        );

    const menuButton =
      (
        selector: string,
      ):
        HTMLButtonElement | null =>
        document.querySelector<
          HTMLButtonElement
        >(
          selector,
        );

    beforeEach(() => {
      TestBed.configureTestingModule({});

      service =
        TestBed.inject(
          ExcelTableEnhancerService,
        );

      document.body.innerHTML = `
        <main class="app-main">
          <div>
            <table data-excel-filter-mode="local">
              <thead>
                <tr>
                  <th>
                    <input
                      type="checkbox"
                      aria-label="Select all">
                  </th>
                  <th>Name</th>
                  <th>Qty</th>
                  <th>Updated Date</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>
                    <input type="checkbox">
                  </td>
                  <td>Beta</td>
                  <td>12</td>
                  <td>07/08/2026</td>
                  <td>View</td>
                </tr>
                <tr>
                  <td>
                    <input type="checkbox">
                  </td>
                  <td>Alpha</td>
                  <td>2</td>
                  <td>01/08/2026</td>
                  <td>View</td>
                </tr>
                <tr>
                  <td>
                    <input type="checkbox">
                  </td>
                  <td>Gamma</td>
                  <td>100</td>
                  <td>15/08/2026</td>
                  <td>View</td>
                </tr>
              </tbody>
            </table>
          </div>
        </main>
      `;

      service.start();
    });

    afterEach(() => {
      service.ngOnDestroy();
      document.body.innerHTML =
        '';
    });

    it(
      'keeps filter state on the actual table column when skipped headers exist',
      async () => {
        const button =
          buttonFor(
            'Name',
          );

        expect(button).not.toBeNull();

        if (!button) {
          return;
        }

        expect(
          button.getAttribute(
            'aria-pressed',
          ),
        ).toBe('false');

        button.click();

        const options =
          Array.from(
            document.querySelectorAll<
              HTMLLabelElement
            >(
              '.po-excel-filter-value',
            ),
          );

        const betaOption =
          options.find(
            option =>
              option.textContent
                ?.includes(
                  'Beta',
                ),
          );

        expect(betaOption).toBeDefined();

        const betaCheckbox =
          betaOption
            ?.querySelector<
              HTMLInputElement
            >(
              'input[type="checkbox"]',
            );

        expect(betaCheckbox).not.toBeNull();

        if (!betaCheckbox) {
          return;
        }

        betaCheckbox.checked =
          false;

        menuButton(
          '.po-excel-filter-apply',
        )?.click();

        expect(
          button.getAttribute(
            'aria-pressed',
          ),
        ).toBe('true');

        const rows =
          Array.from(
            document.querySelectorAll<
              HTMLTableRowElement
            >(
              'tbody tr',
            ),
          );

        const betaRow =
          rows.find(
            row =>
              row.cells
                .item(1)
                ?.textContent
                ?.trim() ===
              'Beta',
          );

        expect(
          betaRow?.classList.contains(
            'po-excel-row-filtered-out',
          ),
        ).toBe(true);

        const mutationTarget =
          document.querySelector(
            '.app-main',
          );

        const marker =
          document.createElement(
            'span',
          );

        marker.textContent =
          'mutation';

        mutationTarget?.appendChild(
          marker,
        );

        await new Promise<void>(
          resolve => {
            setTimeout(
              resolve,
              0,
            );
          },
        );

        expect(
          button.getAttribute(
            'aria-pressed',
          ),
        ).toBe('true');

        expect(
          button.classList.contains(
            'po-excel-filter-button--active',
          ),
        ).toBe(true);
      },
    );

    it(
      'sorts text A to Z and Z to A like Excel',
      () => {
        const button =
          buttonFor(
            'Name',
          );

        expect(button).not.toBeNull();

        button?.click();

        expect(
          menuButton(
            '.po-excel-filter-sort-asc',
          )?.textContent,
        ).toContain(
          'Sort A to Z',
        );

        menuButton(
          '.po-excel-filter-sort-asc',
        )?.click();

        expect(
          rowValues(1),
        ).toEqual(
          [
            'Alpha',
            'Beta',
            'Gamma',
          ],
        );

        expect(
          button?.textContent,
        ).toBe('↑');

        button?.click();

        menuButton(
          '.po-excel-filter-sort-desc',
        )?.click();

        expect(
          rowValues(1),
        ).toEqual(
          [
            'Gamma',
            'Beta',
            'Alpha',
          ],
        );

        expect(
          button?.textContent,
        ).toBe('↓');
      },
    );

    it(
      'uses numeric Smallest to Largest sorting for quantity columns',
      () => {
        const button =
          buttonFor(
            'Qty',
          );

        expect(button).not.toBeNull();

        button?.click();

        expect(
          menuButton(
            '.po-excel-filter-sort-asc',
          )?.textContent,
        ).toContain(
          'Sort Smallest to Largest',
        );

        menuButton(
          '.po-excel-filter-sort-asc',
        )?.click();

        expect(
          rowValues(2),
        ).toEqual(
          [
            '2',
            '12',
            '100',
          ],
        );
      },
    );

    it(
      'uses date Oldest to Newest sorting and exposes searchable contents',
      () => {
        const button =
          buttonFor(
            'Updated Date',
          );

        expect(button).not.toBeNull();

        button?.click();

        expect(
          menuButton(
            '.po-excel-filter-sort-asc',
          )?.textContent,
        ).toContain(
          'Sort Oldest to Newest',
        );

        expect(
          document.querySelector<
            HTMLElement
          >(
            '.po-excel-filter-contents-heading',
          )?.textContent,
        ).toContain(
          'Filter by contents',
        );

        expect(
          document.querySelector<
            HTMLInputElement
          >(
            '.po-excel-filter-search',
          )?.placeholder,
        ).toBe(
          'Search contents',
        );

        menuButton(
          '.po-excel-filter-sort-asc',
        )?.click();

        expect(
          rowValues(3),
        ).toEqual(
          [
            '01/08/2026',
            '07/08/2026',
            '15/08/2026',
          ],
        );
      },
    );

    it(
      'clears sorting back to original local table order',
      () => {
        const button =
          buttonFor(
            'Name',
          );

        button?.click();

        menuButton(
          '.po-excel-filter-sort-asc',
        )?.click();

        expect(
          rowValues(1),
        ).toEqual(
          [
            'Alpha',
            'Beta',
            'Gamma',
          ],
        );

        button?.click();

        menuButton(
          '.po-excel-filter-clear-sort',
        )?.click();

        expect(
          rowValues(1),
        ).toEqual(
          [
            'Beta',
            'Alpha',
            'Gamma',
          ],
        );

        expect(
          button?.textContent,
        ).toBe('▾');
      },
    );
  },
);
