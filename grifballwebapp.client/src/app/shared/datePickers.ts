import { Provider } from '@angular/core';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';

/**
 * The Luxon date adapter and formats for the date-time pickers. Each component with a picker
 * provides it, rather than the whole app, so Luxon is loaded with those pages and not at startup.
 */
export function provideDatePickers(): Provider[] {
  return provideLuxonDatetimeAdapter({
    parse: {
      dateInput: 'yyyy-LL-dd',
      monthInput: 'LLLL',
      yearInput: 'yyyy',
      timeInput: 'HH:mm',
      datetimeInput: 'yyyy-LL-dd HH:mm',
    },
    display: {
      dateInput: 'yyyy-LL-dd',
      monthInput: 'LLLL',
      yearInput: 'yyyy',
      timeInput: 'HH:mm',
      datetimeInput: 'yyyy-LL-dd HH:mm',
      monthYearLabel: 'yyyy LLLL',
      dateA11yLabel: 'DDD',
      monthYearA11yLabel: 'LLLL yyyy',
      popupHeaderDateLabel: 'dd LLL, ccc',
    }
  });
}
