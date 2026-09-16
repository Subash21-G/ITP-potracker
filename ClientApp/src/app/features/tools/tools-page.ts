import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ManageOnlyDirective } from '../../core/directives/manage-only.directive';

type ToolType = 'Insert' | 'Tape' | 'Drill' | 'Center Bit' | 'End Mill' | 'Gauge' | 'Instrument';
interface ToolEntry { id: number; type: ToolType; name: string; cost: number; date: string; quantity: number; }
const STORAGE_KEY = 'po-tracker-tools';

@Component({ selector: 'app-tools-page', imports: [CurrencyPipe, DatePipe, DecimalPipe, ManageOnlyDirective, ReactiveFormsModule], templateUrl: './tools-page.html', styleUrl: './tools-page.css', changeDetection: ChangeDetectionStrategy.OnPush })
export class ToolsPage {
  readonly types: readonly ToolType[] = ['Insert', 'Tape', 'Drill', 'Center Bit', 'End Mill'];
  readonly pageTitle: string;
  readonly pageDescription: string;
  readonly entries = signal<ToolEntry[]>(this.read());
  readonly form = new FormGroup({ type: new FormControl<ToolType>('Insert', { nonNullable: true }), name: new FormControl('', { nonNullable: true, validators: Validators.required }), cost: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0)] }), date: new FormControl('', { nonNullable: true, validators: Validators.required }), quantity: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(1)] }) });
  constructor(route: ActivatedRoute) {
    const category = route.snapshot.data['category'] as string | undefined;
    if (category === 'gauges') { this.types = ['Gauge']; this.pageTitle = 'Gauge Register'; this.pageDescription = 'Track gauge purchases, costs, dates, and quantities.'; }
    else if (category === 'instruments') { this.types = ['Instrument']; this.pageTitle = 'Instrument Register'; this.pageDescription = 'Track instrument purchases, costs, dates, and quantities.'; }
    else { this.pageTitle = 'Tools Register'; this.pageDescription = 'Track purchased machining tools, costs, dates, and available quantities.'; }
  }
  add(): void { if (this.form.invalid) { this.form.markAllAsTouched(); return; } const v = this.form.getRawValue(); const next = [...this.entries(), { id: Date.now(), type: v.type, name: v.name.trim(), cost: Number(v.cost), date: v.date, quantity: Number(v.quantity) }]; this.entries.set(next); this.write(next); this.form.reset({ type: 'Insert', name: '', cost: null, date: '', quantity: null }); }
  remove(entry: ToolEntry): void { if (!window.confirm(`Delete ${entry.name}?`)) return; const next = this.entries().filter(item => item.id !== entry.id); this.entries.set(next); this.write(next); }
  entriesFor(type: ToolType): ToolEntry[] { return this.entries().filter(item => item.type === type); }
  private read(): ToolEntry[] { try { const value = JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]'); return Array.isArray(value) ? value : []; } catch { return []; } }
  private write(value: ToolEntry[]): void { try { localStorage.setItem(STORAGE_KEY, JSON.stringify(value)); } catch { /* storage unavailable */ } }
}
