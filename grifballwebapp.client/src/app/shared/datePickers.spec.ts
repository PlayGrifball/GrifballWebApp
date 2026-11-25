import { TestBed } from '@angular/core/testing';
import { DatetimeAdapter, MTX_DATETIME_FORMATS } from '@ng-matero/extensions/core';
import { LuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { DateTime } from 'luxon';
import { provideDatePickers } from './datePickers';

describe('provideDatePickers', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: provideDatePickers() }));

  it('provides the Luxon adapter for the date-time pickers', () => {
    expect(TestBed.inject(DatetimeAdapter)).toBeInstanceOf(LuxonDatetimeAdapter);
  });

  it('shows and reads dates as the app always has (yyyy-LL-dd HH:mm)', () => {
    const formats = TestBed.inject(MTX_DATETIME_FORMATS);
    expect(formats.display.datetimeInput).toBe('yyyy-LL-dd HH:mm');
    expect(formats.parse.datetimeInput).toBe('yyyy-LL-dd HH:mm');

    const adapter = TestBed.inject(DatetimeAdapter<DateTime>);
    const date = adapter.parse('2026-10-29 18:30', formats.parse.datetimeInput)!;
    expect(adapter.format(date, formats.display.datetimeInput)).toBe('2026-10-29 18:30');
  });
});
