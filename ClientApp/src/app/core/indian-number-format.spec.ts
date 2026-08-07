import {
  formatNumber,
  registerLocaleData,
} from '@angular/common';

import localeEnIn from
  '@angular/common/locales/en-IN';

registerLocaleData(
  localeEnIn,
);

describe(
  'Indian number formatting',
  () => {
    it(
      'uses lakh grouping for material value',
      () => {
        expect(
          formatNumber(
            2259344.64,
            'en-IN',
            '1.2-2',
          ),
        ).toBe(
          '22,59,344.64',
        );
      },
    );

    it(
      'uses crore grouping for large values',
      () => {
        expect(
          formatNumber(
            10000000,
            'en-IN',
            '1.0-0',
          ),
        ).toBe(
          '1,00,00,000',
        );
      },
    );

    it(
      'preserves quantity decimal precision',
      () => {
        expect(
          formatNumber(
            125000.5,
            'en-IN',
            '1.0-2',
          ),
        ).toBe(
          '1,25,000.5',
        );
      },
    );
  },
);