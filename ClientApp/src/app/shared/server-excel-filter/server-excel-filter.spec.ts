import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ServerExcelFilter, ServerExcelSortDirection } from './server-excel-filter';

describe('ServerExcelFilter', () => {
  let fixture: ComponentFixture<ServerExcelFilter>;

  let component: ServerExcelFilter;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ServerExcelFilter],
    }).compileComponents();

    fixture = TestBed.createComponent(ServerExcelFilter);

    component = fixture.componentInstance;

    component.title = 'PO number';

    component.options = [
      {
        value: 'PO-1',
        label: 'PO-1',
        count: 2,
      },
      {
        value: 'PO-2',
        label: 'PO-2',
        count: 1,
      },
    ];

    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
  });

  it('opens and closes with the fallback menu implementation', () => {
    const trigger = fixture.nativeElement.querySelector(
      '.server-excel-filter__trigger',
    ) as HTMLButtonElement;

    const menu = fixture.nativeElement.querySelector(
      '.server-excel-filter__menu',
    ) as HTMLDivElement;

    trigger.click();
    fixture.detectChanges();

    expect(component.isOpen).toBe(true);

    expect(trigger.getAttribute('aria-expanded')).toBe('true');

    trigger.click();
    fixture.detectChanges();

    expect(component.isOpen).toBe(false);

    expect(menu.style.display).toBe('none');
  });

  it('shows the current sort on the header and emits Clear sort', () => {
    fixture.componentRef.setInput('sortDirection', 'asc');

    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector(
      '.server-excel-filter__trigger',
    ) as HTMLButtonElement;

    expect(trigger.textContent?.trim()).toBe('↑');

    expect(trigger.getAttribute('aria-pressed')).toBe('true');

    let requested: ServerExcelSortDirection = 'asc';

    component.sortRequested.subscribe((direction) => {
      requested = direction;
    });

    trigger.click();
    fixture.detectChanges();

    const clearSort = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
        '.server-excel-filter__menu-action',
      ),
    ).find((button) => button.textContent?.includes('Clear sort'));

    clearSort?.click();

    expect(requested).toBeNull();
  });

  it('reports and applies the draft selection', () => {
    let applied: readonly string[] = [];

    component.applyValues.subscribe((values) => {
      applied = values;
    });

    const trigger = fixture.nativeElement.querySelector(
      '.server-excel-filter__trigger',
    ) as HTMLButtonElement;

    trigger.click();
    fixture.detectChanges();

    const firstCheckbox = fixture.nativeElement.querySelector(
      '.server-excel-filter__option input',
    ) as HTMLInputElement;

    firstCheckbox.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('1 of 2 selected');

    const apply = fixture.nativeElement.querySelector(
      '.server-excel-filter__button--primary',
    ) as HTMLButtonElement;

    apply.click();

    expect(applied).toEqual(['PO-2']);
  });

  it('lists numeric labels naturally and checks every value by default', () => {
    component.options = [
      { value: 'N-100', label: '100' },
      { value: 'N-10', label: '10' },
      { value: 'N-20', label: '20' },
    ];

    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector(
      '.server-excel-filter__trigger',
    ) as HTMLButtonElement;

    trigger.click();
    fixture.detectChanges();

    const labels = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>(
        '.server-excel-filter__option-label',
      ),
    ).map((label) => label.textContent?.trim());
    const checkboxes = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>(
        '.server-excel-filter__option input',
      ),
    );

    expect(labels).toEqual(['10', '20', '100']);
    expect(checkboxes.every((checkbox) => checkbox.checked)).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('3 of 3 selected');
  });
});
