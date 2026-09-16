export type MasterSheetProcessSource = 'In-house' | 'Outsource';

export interface MasterSheetProcessStep {
  id: number;
  sequence: number;
  processName: string;
  vendorName: string;
  source: MasterSheetProcessSource;
}

export interface MasterSheetRecord {
  id: number;
  materialNo: string;
  shortDescription: string;
  grade: string;
  drawingNo: string;
  drawingVersion: string;
  finishSize: string;
  requiredRmSize: string;
  processSteps: readonly MasterSheetProcessStep[];
  createdDate: string;
  updatedDate: string | null;
}

export interface MasterSheetProcessStepRequest {
  processName: string;
  vendorName: string;
  source: MasterSheetProcessSource;
}

export interface MasterSheetSaveRequest {
  materialNo: string;
  shortDescription: string;
  grade: string;
  drawingNo: string;
  drawingVersion: string;
  finishSize: string;
  requiredRmSize: string;
  processSteps: readonly MasterSheetProcessStepRequest[];
}

export interface MasterSheetProcessOption {
  id: number;
  processName: string;
  vendorName: string;
  source: MasterSheetProcessSource;
}

export interface MasterSheetProcessOptionRequest {
  processName: string;
  vendorName: string;
  source: MasterSheetProcessSource;
}

export interface MasterSheetDeleteResponse {
  success: boolean;
  materialNo: string;
  message: string;
}
