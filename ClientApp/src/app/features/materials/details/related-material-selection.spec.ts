import { MaterialListItem } from '../../../core/models/material.model';
import { findRelatedMaterial } from './related-material-selection';

describe('findRelatedMaterial', () => {
  const materials = [
    {
      id: 111,
      purchaseOrderId: 10,
      poNumber: 'PO-10',
      materialNo: 'MAT-100',
    },
    {
      id: 222,
      purchaseOrderId: 20,
      poNumber: 'PO-20',
      materialNo: 'MAT-100',
    },
  ] as MaterialListItem[];

  it('returns the same material from the selected purchase order', () => {
    expect(findRelatedMaterial(materials, 20, 'MAT-100')?.id).toBe(222);
  });

  it('matches material numbers after trimming and normalizing case', () => {
    expect(findRelatedMaterial(materials, 10, '  mat-100  ')?.id).toBe(111);
  });

  it('does not return a partial or different material number', () => {
    expect(findRelatedMaterial(materials, 20, 'MAT-10')).toBeUndefined();
  });
});
