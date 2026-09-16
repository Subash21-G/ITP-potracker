import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  forkJoin,
  of,
  switchMap,
  tap,
  throwError,
} from 'rxjs';
import {
  MasterSheetProcessOption,
  MasterSheetProcessOptionRequest,
  MasterSheetProcessSource,
  MasterSheetProcessStep,
  MasterSheetRecord,
  MasterSheetSaveRequest,
} from '../../core/models/mastersheet-api.model';
import { MaterialListItem } from '../../core/models/material.model';
import { MasterSheetApiService } from '../../core/services/mastersheet-api.service';
import { MaterialCrudApiService } from '../../core/services/material-crud-api.service';
import { MaterialsApiService } from '../../core/services/materials-api.service';

@Component({
  selector: 'app-mastersheet-page',
  imports: [ReactiveFormsModule, RouterLink, RouterLinkActive],
  templateUrl: './mastersheet-page.html',
  styleUrl: './mastersheet-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MastersheetPage {
  private readonly routeParams = toSignal(inject(ActivatedRoute).paramMap);
  readonly isMaterialPage = computed(() => this.routeParams()?.get('section') === 'materials');
  private readonly materialsApi = inject(MaterialsApiService);
  private readonly detailsApi = inject(MaterialCrudApiService);
  private readonly masterSheetApi = inject(MasterSheetApiService);
  private lookupVersion = 0;

  readonly materials = signal<readonly MaterialListItem[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly flow = signal<readonly MasterSheetProcessOption[]>([]);
  readonly processOptions = signal<readonly string[]>([]);
  readonly vendorOptions = signal<readonly string[]>([]);
  readonly materialAssignments = signal<readonly MasterSheetProcessStep[]>([]);
  readonly loadedRecord = signal<MasterSheetRecord | null>(null);
  readonly savedRecords = signal<readonly MasterSheetRecord[]>([]);
  readonly pageSizes = [10, 25, 50, 100];
  readonly processPageSize = signal(10);
  readonly materialPageSize = signal(10);
  readonly processSearch = signal('');
  readonly materialSearch = signal('');
  readonly filteredProcesses = computed(() => {
    const query = this.processSearch().trim().toLocaleLowerCase();
    return this.flow().filter(row => this.matchesSearch(query, [
      row.processName, row.vendorName || 'No vendor', row.source,
    ]));
  });
  readonly filteredRecords = computed(() => {
    const query = this.materialSearch().trim().toLocaleLowerCase();
    return this.savedRecords().filter(record => this.matchesSearch(query, [
      record.materialNo, record.shortDescription, record.grade, record.drawingNo, record.drawingVersion,
      record.finishSize, record.requiredRmSize,
      ...record.processSteps.flatMap(step => [step.processName, step.vendorName || 'No vendor', step.source]),
    ]));
  });
  readonly processTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredProcesses().length / this.processPageSize())));
  readonly materialTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredRecords().length / this.materialPageSize())));
  readonly processPage = linkedSignal<number, number>({
    source: this.processTotalPages,
    computation: (total, previous) => Math.min(previous?.value ?? 1, total),
  });
  readonly materialPage = linkedSignal<number, number>({
    source: this.materialTotalPages,
    computation: (total, previous) => Math.min(previous?.value ?? 1, total),
  });
  readonly pagedProcesses = computed(() => this.filteredProcesses().slice(
    (this.processPage() - 1) * this.processPageSize(), this.processPage() * this.processPageSize(),
  ));
  readonly pagedRecords = computed(() => this.filteredRecords().slice(
    (this.materialPage() - 1) * this.materialPageSize(), this.materialPage() * this.materialPageSize(),
  ));

  changeSearch(section: 'process' | 'material', value: string): void {
    if (section === 'process') {
      this.processSearch.set(value);
      this.processPage.set(1);
    } else {
      this.materialSearch.set(value);
      this.materialPage.set(1);
    }
  }

  private matchesSearch(query: string, values: readonly (string | null | undefined)[]): boolean {
    return !query || values.some(value => value?.toLocaleLowerCase().includes(query));
  }

  changePageSize(section: 'process' | 'material', value: string): void {
    const size = Number(value);
    if (!this.pageSizes.includes(size)) return;
    if (section === 'process') {
      this.processPageSize.set(size);
      this.processPage.set(1);
    } else {
      this.materialPageSize.set(size);
      this.materialPage.set(1);
    }
  }

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    vendor: new FormControl('', { nonNullable: true }),
    source: new FormControl<MasterSheetProcessSource>('In-house', {
      nonNullable: true,
    }),
  });

  readonly masterForm = new FormGroup({
    materialNo: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    shortDescription: new FormControl('', { nonNullable: true }),
    grade: new FormControl('', { nonNullable: true }),
    drawingNo: new FormControl('', { nonNullable: true }),
    drawingVersion: new FormControl('', { nonNullable: true }),
    finishSize: new FormControl('', { nonNullable: true }),
    requiredRmSize: new FormControl('', { nonNullable: true }),
    process: new FormControl('', { nonNullable: true }),
    vendor: new FormControl('', { nonNullable: true }),
    source: new FormControl<MasterSheetProcessSource>('In-house', {
      nonNullable: true,
    }),
  });

  constructor() {
    this.loadProcessOptions();
    this.loadSavedRecords();

    this.masterForm.controls.materialNo.valueChanges
      .pipe(
        distinctUntilChanged(),
        tap(() => {
          // Invalidate the old request immediately, before the debounce expires.
          ++this.lookupVersion;
          this.clearMaterialDetails();
          this.loading.set(!!this.masterForm.controls.materialNo.value.trim());
        }),
        debounceTime(450),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.lookupMaster());
  }

  private loadSavedRecords(): void {
    this.masterSheetApi.getRecords().subscribe({
      next: (records) => this.savedRecords.set(records),
      // Older deployments do not have the records-list endpoint yet. The
      // material lookup still works there, so do not block the page with a
      // global error while that deployment is being upgraded.
      error: () => this.savedRecords.set(this.readLegacyRecords()),
    });
  }

  lookupMaster(): void {
    const materialNo = this.masterForm.controls.materialNo.value.trim();
    const lookupVersion = ++this.lookupVersion;

    if (!materialNo) {
      this.clearMaterialDetails();
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.message.set('');

    this.materialsApi
      .getMaterials(
        {
          page: 1,
          pageSize: 100,
          search: materialNo,
          removeDuplicates: true,
        },
        true,
      )
      .pipe(
        switchMap((result) => {
          const selectedMaterial =
            result.items.find(
              (item) =>
                item.materialNo.localeCompare(materialNo, undefined, {
                  sensitivity: 'accent',
                }) === 0,
            ) ?? null;

          return forkJoin({
            selectedMaterial: of(selectedMaterial),
            detail: selectedMaterial
              ? this.detailsApi
                  .getDetails(selectedMaterial.id, true)
                  .pipe(catchError(() => of(null)))
              : of(null),
            record: this.masterSheetApi
              .getRecord(materialNo)
              .pipe(
                catchError((lookupError: HttpErrorResponse) =>
                  lookupError.status === 404 ? of(null) : throwError(() => lookupError),
                ),
              ),
          });
        }),
      )
      .subscribe({
        next: ({ selectedMaterial, detail, record }) => {
          if (lookupVersion !== this.lookupVersion) return;

          const legacyRecord = record ? null : this.readLegacyRecord(materialNo);
          const effectiveRecord = record ?? legacyRecord;

          this.materials.set(selectedMaterial ? [selectedMaterial] : []);

          this.masterForm.patchValue(
            {
              materialNo: effectiveRecord?.materialNo ?? materialNo,
              shortDescription: effectiveRecord?.shortDescription ?? detail?.shortDescription ?? '',
              grade: effectiveRecord?.grade ?? detail?.rmGrade ?? '',
              drawingNo: effectiveRecord?.drawingNo || detail?.drawingNo || '',
              drawingVersion: effectiveRecord?.drawingVersion ?? detail?.drawingVersion ?? '',
              finishSize: effectiveRecord?.finishSize ?? detail?.surfaceFinish ?? '',
              requiredRmSize: effectiveRecord?.requiredRmSize ?? detail?.rmSize ?? '',
              process: '',
              vendor: '',
              source: 'In-house',
            },
            { emitEvent: false },
          );

          this.materialAssignments.set(effectiveRecord?.processSteps ?? []);
          this.loadedRecord.set(record);
          this.loading.set(false);

          if (legacyRecord) {
            this.saveMaster('Existing material data moved to the database.');
          }
        },
        error: () => {
          if (lookupVersion !== this.lookupVersion) return;
          this.error.set('Mastersheet data could not be loaded.');
          this.loading.set(false);
        },
      });
  }

  private clearMaterialDetails(): void {
    this.materials.set([]);
    this.materialAssignments.set([]);
    this.loadedRecord.set(null);
    this.masterForm.patchValue({
      shortDescription: '', grade: '', drawingNo: '', drawingVersion: '', finishSize: '', requiredRmSize: '',
      process: '', vendor: '', source: 'In-house',
    }, { emitEvent: false });
  }

  saveMaster(successMessage = 'Master sheet data saved.'): void {
    if (this.loading() || this.saving()) return;
    const request = this.buildSaveRequest();

    if (!request) {
      this.masterForm.controls.materialNo.markAsTouched();
      this.error.set('Material number is required.');
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.message.set('');

    this.masterSheetApi.saveRecord(request).subscribe({
      next: (record) => {
        this.loadedRecord.set(record);
        this.savedRecords.set([
          ...this.savedRecords().filter((item) => item.materialNo !== record.materialNo),
          record,
        ].sort((left, right) => left.materialNo.localeCompare(right.materialNo)));
        this.materialAssignments.set(record.processSteps);
        this.masterForm.patchValue(
          {
            materialNo: record.materialNo,
            shortDescription: record.shortDescription,
            grade: record.grade,
            drawingNo: record.drawingNo ?? '',
            drawingVersion: record.drawingVersion,
            finishSize: record.finishSize,
            requiredRmSize: record.requiredRmSize,
          },
          { emitEvent: false },
        );
        this.message.set(successMessage);
        this.clearLegacyRecord(record.materialNo);
        this.saving.set(false);
      },
      error: () => {
        const localRecord = this.buildLocalRecord(request);
        this.storeLegacyRecord(localRecord);
        this.savedRecords.set([
          ...this.savedRecords().filter((item) => item.materialNo !== localRecord.materialNo),
          localRecord,
        ].sort((left, right) => left.materialNo.localeCompare(right.materialNo)));
        this.loadedRecord.set(localRecord);
        this.materialAssignments.set(localRecord.processSteps);
        this.message.set('Saved locally. It will sync to the database after the server is updated.');
        this.error.set('Master sheet data could not be saved.');
        this.saving.set(false);
      },
    });
  }

  addMaterialProcess(): void {
    if (this.loading() || this.saving()) return;
    const processName = this.masterForm.controls.process.value.trim();

    if (!this.masterForm.controls.materialNo.value.trim() || !processName) {
      this.error.set('Select a material number and process first.');
      return;
    }

    const current = this.materialAssignments();
    const next: readonly MasterSheetProcessStep[] = [
      ...current,
      {
        id: 0,
        sequence: current.length + 1,
        processName,
        vendorName: this.masterForm.controls.vendor.value.trim(),
        source: this.masterForm.controls.source.value,
      },
    ];

    this.materialAssignments.set(next);
    this.masterForm.patchValue({
      process: '',
      vendor: '',
      source: 'In-house',
    });
    this.saveMaster(`Process ${next.length} added and saved.`);
  }

  removeMaterialProcess(index: number): void {
    if (this.loading() || this.saving()) return;
    const next = this.materialAssignments()
      .filter((_, itemIndex) => itemIndex !== index)
      .map((step, itemIndex) => ({ ...step, sequence: itemIndex + 1 }));

    this.materialAssignments.set(next);
    this.saveMaster('Process flow updated.');
  }

  deleteMaster(): void {
    const materialNo = this.masterForm.controls.materialNo.value.trim();

    if (!materialNo) return;

    this.saving.set(true);
    this.error.set('');
    this.message.set('');

    this.masterSheetApi.deleteRecord(materialNo).subscribe({
      next: (response) => {
        this.loadedRecord.set(null);
        this.materialAssignments.set([]);
        this.materials.set([]);
        this.savedRecords.set(
          this.savedRecords().filter((record) => record.materialNo !== materialNo),
        );
        this.masterForm.reset({
          materialNo: '',
          shortDescription: '',
          grade: '',
          drawingNo: '',
          drawingVersion: '',
          finishSize: '',
          requiredRmSize: '',
          process: '',
          vendor: '',
          source: 'In-house',
        });
        this.message.set(response.message);
        this.saving.set(false);
      },
      error: (deleteError: HttpErrorResponse) => {
        this.error.set(
          deleteError.status === 404
            ? 'No saved master sheet data was found.'
            : 'Master sheet data could not be deleted.',
        );
        this.saving.set(false);
      },
    });
  }

  deleteSavedRecord(record: MasterSheetRecord): void {
    if (!window.confirm(`Delete master data for ${record.materialNo}?`)) return;

    this.saving.set(true);
    this.error.set('');
    this.message.set('');
    this.masterSheetApi.deleteRecord(record.materialNo).subscribe({
      next: (response) => {
        this.savedRecords.set(this.savedRecords().filter((item) => item.materialNo !== record.materialNo));
        if (this.loadedRecord()?.materialNo === record.materialNo) {
          this.loadedRecord.set(null);
          this.materialAssignments.set([]);
        }
        this.clearLegacyRecord(record.materialNo);
        this.message.set(response.message);
        this.saving.set(false);
      },
      error: (deleteError: HttpErrorResponse) => {
        if (deleteError.status === 404) {
          this.savedRecords.set(this.savedRecords().filter((item) => item.materialNo !== record.materialNo));
          this.clearLegacyRecord(record.materialNo);
          this.message.set('Local master data deleted.');
        } else {
          this.error.set('Master data could not be deleted.');
        }
        this.saving.set(false);
      },
    });
  }

  addProcess(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.error.set('');
    this.message.set('');

    this.masterSheetApi
      .addProcessOption({
        processName: this.form.controls.name.value.trim(),
        vendorName: this.form.controls.vendor.value.trim(),
        source: this.form.controls.source.value,
      })
      .subscribe({
        next: (option) => {
          const next = [...this.flow(), option];
          this.flow.set(next);
          this.refreshOptions(next);
          this.form.reset({ name: '', vendor: '', source: 'In-house' });
          this.message.set('Process and vendor option saved.');
        },
        error: () => {
          this.error.set('Process and vendor option could not be saved.');
        },
      });
  }

  removeProcess(option: MasterSheetProcessOption): void {
    this.masterSheetApi.deleteProcessOption(option.id).subscribe({
      next: () => {
        const next = this.flow().filter((item) => item.id !== option.id);
        this.flow.set(next);
        this.refreshOptions(next);
        this.message.set('Process option removed.');
      },
      error: () => {
        this.error.set('Process option could not be removed.');
      },
    });
  }

  processSummary(): string {
    return this.materialAssignments().length
      ? this.materialAssignments()
          .map((step) => `Process ${step.sequence}: ${step.processName}`)
          .join(', ')
      : '-';
  }

  vendorSummary(): string {
    return this.materialAssignments().length
      ? this.materialAssignments()
          .map((step) => step.vendorName || 'No vendor')
          .join(', ')
      : '-';
  }

  recordProcessSummary(record: MasterSheetRecord): string {
    return record.processSteps.length
      ? record.processSteps.map((step) => `Process ${step.sequence}: ${step.processName}`).join(', ')
      : '-';
  }

  recordVendorSummary(record: MasterSheetRecord): string {
    return record.processSteps.length
      ? record.processSteps.map((step) => step.vendorName || 'No vendor').join(', ')
      : '-';
  }

  vendorsForSelectedProcess(): readonly string[] {
    const selectedProcess = this.masterForm.controls.process.value.trim().toLocaleLowerCase();
    if (!selectedProcess) return this.vendorOptions();

    return [
      ...new Set(
        this.flow()
          .filter((option) => option.processName.trim().toLocaleLowerCase() === selectedProcess)
          .map((option) => option.vendorName.trim())
          .filter(Boolean),
      ),
    ];
  }

  onProcessSelected(): void {
    const selectedVendor = this.masterForm.controls.vendor.value.trim();
    if (selectedVendor && !this.vendorsForSelectedProcess().includes(selectedVendor)) {
      this.masterForm.controls.vendor.setValue('');
    }
  }

  private loadProcessOptions(): void {
    this.masterSheetApi.getProcessOptions().subscribe({
      next: (options) => {
        const legacyOptions = this.readLegacyProcessOptions();
        const missingOptions = legacyOptions.filter(
          (legacyOption) => !options.some((option) => this.sameProcessOption(option, legacyOption)),
        );

        if (!missingOptions.length) {
          this.setProcessOptions(options);
          this.clearLegacyProcessOptions();
          return;
        }

        forkJoin(
          missingOptions.map((option) => this.masterSheetApi.addProcessOption(option)),
        ).subscribe({
          next: (migratedOptions) => {
            this.setProcessOptions([...options, ...migratedOptions]);
            this.clearLegacyProcessOptions();
            this.message.set('Existing process and vendor data moved to the database.');
          },
          error: () => {
            this.setProcessOptions(options);
            this.error.set('Existing process and vendor data could not be migrated.');
          },
        });
      },
      error: () => {
        this.error.set('Process and vendor options could not be loaded.');
      },
    });
  }

  private buildSaveRequest(): MasterSheetSaveRequest | null {
    const values = this.masterForm.getRawValue();
    const materialNo = values.materialNo.trim();

    if (!materialNo) return null;

    return {
      materialNo,
      shortDescription: values.shortDescription.trim(),
      grade: values.grade.trim(),
      drawingNo: values.drawingNo.trim(),
      drawingVersion: values.drawingVersion.trim(),
      finishSize: values.finishSize.trim(),
      requiredRmSize: values.requiredRmSize.trim(),
      processSteps: this.materialAssignments().map((step) => ({
        processName: step.processName,
        vendorName: step.vendorName,
        source: step.source,
      })),
    };
  }

  private refreshOptions(rows: readonly MasterSheetProcessOption[]): void {
    this.processOptions.set([...new Set(rows.map((row) => row.processName).filter(Boolean))]);
    this.vendorOptions.set([...new Set(rows.map((row) => row.vendorName).filter(Boolean))]);
  }

  private setProcessOptions(options: readonly MasterSheetProcessOption[]): void {
    this.flow.set(options);
    this.refreshOptions(options);
  }

  private readLegacyProcessOptions(): readonly MasterSheetProcessOptionRequest[] {
    try {
      const rows = JSON.parse(
        localStorage.getItem('po-tracker-mastersheet-flow') || '[]',
      ) as readonly Record<string, unknown>[];

      if (!Array.isArray(rows)) return [];

      return rows
        .map((row) => ({
          processName: typeof row['name'] === 'string' ? row['name'].trim() : '',
          vendorName: typeof row['vendor'] === 'string' ? row['vendor'].trim() : '',
          source: this.normalizeSource(row['source']),
        }))
        .filter((row) => row.processName.length > 0);
    } catch {
      return [];
    }
  }

  private readLegacyRecord(materialNo: string): MasterSheetRecord | null {
    try {
      const dataValue = localStorage.getItem(`po-tracker-master-${materialNo}`);
      const flowValue = localStorage.getItem(`po-tracker-master-flow-${materialNo}`);

      if (!dataValue && !flowValue) return null;

      const data = dataValue ? (JSON.parse(dataValue) as Record<string, unknown>) : {};
      const flow = flowValue ? (JSON.parse(flowValue) as readonly Record<string, unknown>[]) : [];
      const processSteps = (Array.isArray(flow) ? flow : [])
        .map((row, index) => ({
          id: 0,
          sequence: index + 1,
          processName: typeof row['name'] === 'string' ? row['name'].trim() : '',
          vendorName: typeof row['vendor'] === 'string' ? row['vendor'].trim() : '',
          source: this.normalizeSource(row['source']),
        }))
        .filter((step) => step.processName.length > 0);

      if (!processSteps.length && typeof data['process'] === 'string') {
        const processName = data['process'].trim();

        if (processName) {
          processSteps.push({
            id: 0,
            sequence: 1,
            processName,
            vendorName: typeof data['vendor'] === 'string' ? data['vendor'].trim() : '',
            source: 'In-house',
          });
        }
      }

      return {
        id: 0,
        materialNo,
        shortDescription: this.legacyString(data, 'shortDescription'),
        grade: this.legacyString(data, 'grade'),
        drawingNo: this.legacyString(data, 'drawingNo'),
        drawingVersion: this.legacyString(data, 'drawingVersion'),
        finishSize: this.legacyString(data, 'finishSize'),
        requiredRmSize: this.legacyString(data, 'requiredRmSize'),
        processSteps,
        createdDate: '',
        updatedDate: null,
      };
    } catch {
      return null;
    }
  }

  private readLegacyRecords(): readonly MasterSheetRecord[] {
    try {
      const records: MasterSheetRecord[] = [];
      for (let index = 0; index < localStorage.length; index += 1) {
        const key = localStorage.key(index);
        if (!key || !key.startsWith('po-tracker-master-') || key.startsWith('po-tracker-master-flow-')) continue;
        const materialNo = key.slice('po-tracker-master-'.length).trim();
        const record = materialNo ? this.readLegacyRecord(materialNo) : null;
        if (record) records.push(record);
      }
      return records.sort((left, right) => left.materialNo.localeCompare(right.materialNo));
    } catch {
      return [];
    }
  }

  private buildLocalRecord(request: MasterSheetSaveRequest): MasterSheetRecord {
    return {
      id: 0,
      materialNo: request.materialNo,
      shortDescription: request.shortDescription,
      grade: request.grade,
      drawingNo: request.drawingNo,
      drawingVersion: request.drawingVersion,
      finishSize: request.finishSize,
      requiredRmSize: request.requiredRmSize,
      processSteps: request.processSteps.map((step, index) => ({ ...step, id: 0, sequence: index + 1 })),
      createdDate: new Date().toISOString(),
      updatedDate: new Date().toISOString(),
    };
  }

  private storeLegacyRecord(record: MasterSheetRecord): void {
    try {
      localStorage.setItem(`po-tracker-master-${record.materialNo}`, JSON.stringify(record));
      localStorage.setItem(
        `po-tracker-master-flow-${record.materialNo}`,
        JSON.stringify(record.processSteps.map((step) => ({
          name: step.processName,
          vendor: step.vendorName,
          source: step.source,
        }))),
      );
    } catch {
      // Continue showing the in-memory record if browser storage is unavailable.
    }
  }

  private sameProcessOption(
    left: MasterSheetProcessOption,
    right: MasterSheetProcessOptionRequest,
  ): boolean {
    return (
      left.processName.trim().toLocaleLowerCase() ===
        right.processName.trim().toLocaleLowerCase() &&
      left.vendorName.trim().toLocaleLowerCase() === right.vendorName.trim().toLocaleLowerCase() &&
      left.source === right.source
    );
  }

  private normalizeSource(value: unknown): MasterSheetProcessSource {
    return value === 'Outsource' ? 'Outsource' : 'In-house';
  }

  private legacyString(data: Record<string, unknown>, key: string): string {
    return typeof data[key] === 'string' ? data[key].trim() : '';
  }

  private clearLegacyProcessOptions(): void {
    try {
      localStorage.removeItem('po-tracker-mastersheet-flow');
    } catch {
      // Browser storage can be unavailable in restricted browsing modes.
    }
  }

  private clearLegacyRecord(materialNo: string): void {
    try {
      localStorage.removeItem(`po-tracker-master-${materialNo}`);
      localStorage.removeItem(`po-tracker-master-flow-${materialNo}`);
    } catch {
      // Browser storage can be unavailable in restricted browsing modes.
    }
  }
}
