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
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>
                    <input type="checkbox">
                  </td>
                  <td>Alpha</td>
                  <td>View</td>
                </tr>
                <tr>
                  <td>
                    <input type="checkbox">
                  </td>
                  <td>Beta</td>
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
      document.body.innerHTML = '';
    });

    it(
      'keeps the active state on the actual table column when skipped headers exist',
      async () => {
        const button =
          document.querySelector<
            HTMLButtonElement
          >(
            '.po-excel-filter-button',
          );

        expect(button).not.toBeNull();

        if (!button) {
          return;
        }

        expect(
          button
            .closest('th')
            ?.textContent,
        ).toContain('Name');

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
          options.find(option =>
            option.textContent
              ?.includes('Beta'),
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

        betaCheckbox.checked = false;

        const applyButton =
          document.querySelector<
            HTMLButtonElement
          >(
            '.po-excel-filter-apply',
          );

        expect(applyButton).not.toBeNull();

        applyButton?.click();

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

        expect(
          rows[0].classList.contains(
            'po-excel-row-filtered-out',
          ),
        ).toBe(false);

        expect(
          rows[1].classList.contains(
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
  },
);