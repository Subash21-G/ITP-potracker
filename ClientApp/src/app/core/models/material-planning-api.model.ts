import { MaterialPriorityLevel, MaterialSourcingType } from './material.model';

export type MaterialPlanningWorkspace =
  | 'sourcing'
  | 'cost-variation'
  | 'priority-urgent';

export interface MaterialPlanningSummary {
  readonly totalMaterials: number;
  readonly inhouse: number;
  readonly outsourcing: number;
  readonly costVariation: number;
  readonly priority: number;
  readonly urgent: number;
}

export interface MaterialPlanningItem {
  readonly id: number;
  readonly purchaseOrderId: number;
  readonly poNumber: string;
  readonly lineItem: string;
  readonly materialNo: string;
  readonly description: string;
  readonly quantity: number;
  readonly unit: string;
  readonly cost: number;
  readonly sourcingType: MaterialSourcingType;
  readonly outsourcedTo: string;
  readonly hasCostVariation: boolean;
  readonly variationCost: number | null;
  readonly priorityLevel: MaterialPriorityLevel;
  readonly processStatus: string;
  readonly deliveryDate: string | null;
}

export interface MaterialPlanningListResponse {
  readonly summary: MaterialPlanningSummary;
  readonly items: readonly MaterialPlanningItem[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalRecords: number;
  readonly totalPages: number;
}

export interface MaterialPlanningQuery {
  readonly workspace: MaterialPlanningWorkspace;
  readonly scope?: string;
  readonly search?: string;
  readonly page: number;
  readonly pageSize: number;
}
