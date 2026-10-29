import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ToolsPage } from './tools-page';

describe('ToolsPage register categories', () => {
  beforeEach(() => localStorage.removeItem('po-tracker-tools'));
  afterEach(() => {
    localStorage.removeItem('po-tracker-tools');
    TestBed.resetTestingModule();
  });

  it.each([
    ['tools', 'Insert'],
    ['gauges', 'Gauge'],
    ['instruments', 'Instrument'],
  ] as const)('keeps successive purchases in the %s register', (category, type) => {
    TestBed.configureTestingModule({
      providers: [{ provide: ActivatedRoute, useValue: { snapshot: { data: { category } } } }],
    });
    const page = TestBed.runInInjectionContext(() => new ToolsPage());

    for (const name of ['First purchase', 'Second purchase']) {
      expect(page.form.controls.type.value).toBe(type);
      page.form.patchValue({ name, cost: 100, date: '2026-09-24', quantity: 1 });
      page.add();
    }

    expect(page.entriesFor(type).map(entry => entry.name)).toEqual(['First purchase', 'Second purchase']);
    expect(JSON.parse(localStorage.getItem('po-tracker-tools')!).map((entry: { type: string }) => entry.type))
      .toEqual([type, type]);
    expect(page.form.controls.type.value).toBe(type);
  });
});
