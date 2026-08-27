import { MaterialListItem } from '../../../core/models/material.model';

export function findRelatedMaterial(
  materials: readonly MaterialListItem[],
  purchaseOrderId: number,
  materialNumber: string,
): MaterialListItem | undefined {
  const normalizedMaterialNumber = materialNumber.trim().toLocaleLowerCase();

  return materials.find(
    (material) =>
      material.purchaseOrderId === purchaseOrderId &&
      material.materialNo.trim().toLocaleLowerCase() === normalizedMaterialNumber,
  );
}
