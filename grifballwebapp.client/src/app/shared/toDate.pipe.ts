import { Pipe, PipeTransform } from '@angular/core';
import { DateTime } from 'luxon';

/** Hands a Luxon DateTime to Angular's date pipe: `{{ x | toDate | date:'short' }}`. */
@Pipe({ name: 'toDate' })
export class ToDatePipe implements PipeTransform {
  transform(value: DateTime | null | undefined): Date | null {
    return value ? value.toJSDate() : null;
  }
}
