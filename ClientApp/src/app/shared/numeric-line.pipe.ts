import { Pipe, PipeTransform } from '@angular/core';

/** Displays the numeric PO line reference, ignoring any status prefix. */
@Pipe({ name: 'numericLine', standalone: true })
export class NumericLinePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return (value ?? '').replace(/[^0-9]/g, '') || '\u2014';
  }
}
