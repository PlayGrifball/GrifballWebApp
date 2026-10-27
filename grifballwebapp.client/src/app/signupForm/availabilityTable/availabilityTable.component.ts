import { Component, ChangeDetectionStrategy, computed, inject, model } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTableModule } from '@angular/material/table';
import { TimeslotDto } from '../../api/dtos/signupResponseDto';
import { AvailabilityService } from '../../availability.service';
import { FormsModule } from '@angular/forms';

/** A grid cell: a copy of a timeslot (with its display time), a day-of-week header, or a filler. */
export interface TimeslotCell extends TimeslotDto {
  isDisabled: boolean;
  isHeader: boolean;
}

@Component({
    selector: 'app-availability-table',
    imports: [
        MatCheckboxModule,
        MatTableModule,
        FormsModule
    ],
    templateUrl: './availabilityTable.component.html',
    styleUrl: './availabilityTable.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class AvailabilityTableComponent {
  private availabilityService = inject(AvailabilityService);


  DayOfWeek: string = this.availabilityService.DayOfWeek;

  private parseTime(time: string): string {
    return this.availabilityService.parseTime(time);
  }

  // The parent binds this before its HTTP request has resolved, so null/undefined
  // must be treated as "no timeslots yet" rather than throwing.
  // The grid is built from new cell objects; the timeslots themselves are never mutated.
  // Ticking a box emits a new list (timeslotsChange) with the changed slots replaced.
  readonly timeslots = model.required<TimeslotDto[] | null | undefined>();

  private readonly table = computed(() => this.buildTable(this.timeslots()));
  readonly timeColumns = computed(() => this.table().timeColumns);
  readonly dtos = computed(() => this.table().dtos);

  private buildTable(value: TimeslotDto[] | null | undefined): { timeColumns: string[], dtos: TimeslotCell[][] } {
    const timeslots = value ?? [];
    const uniqueDaysOfWeek = [...new Set(timeslots.map(item => item.dayOfWeek))];

    // Parse each time once, and index the slots by day and display time for the grid below.
    const displayTime = new Map([...new Set(timeslots.map(t => t.time))].map(time => [time, this.parseTime(time)]));
    const byCell = new Map(timeslots.map(t => [`${t.dayOfWeek}|${displayTime.get(t.time)}`, t]));

    const timeColumns = [this.DayOfWeek, ...new Set(timeslots.map(item => displayTime.get(item.time)!))];

    const rows: TimeslotCell[][] = [];

    // Create a new row for each day of the week
    uniqueDaysOfWeek.forEach((dayOfWeek, index, arr) => {
      const row: TimeslotCell[] = [];

      const header: TimeslotCell = { optionID: 0, dayOfWeek, time: dayOfWeek.toString(), isChecked: false, isDisabled: false, isHeader: true };

      // Create a new cell for each time slot, disabled if not a valid timeslot in backend
      timeColumns.forEach((time, index2, arr2) => {

        if (time === this.DayOfWeek) {
          row.push(header);
          return;
        }

        const source = byCell.get(`${dayOfWeek}|${time}`);

        let cell: TimeslotCell;
        if (source === undefined) {
          cell = { optionID: 0, dayOfWeek, time, isChecked: false, isDisabled: true, isHeader: false };
        } else {
          // A copy with the display time and the values not sent from the api
          cell = { ...source, isDisabled: false, isHeader: false, time };
        }

        row.push(cell);
      });
      rows.push(row);
    });

    return { timeColumns, dtos: rows };
  }


  public getCell(time: string, slots: TimeslotCell[]): TimeslotCell {

    if (time === this.availabilityService.DayOfWeek)
      return slots[0];
    
    const c = slots.find(x => x.time === time);
    if (c !== undefined) {
      return c;
    }
    return { optionID: 0, dayOfWeek: '', time, isChecked: false, isDisabled: true, isHeader: false };
  }

  /** Sets the checked state of the timeslots behind `cells` (headers and fillers are skipped) and emits the new list. */
  public setChecked(cells: TimeslotCell[], checked: boolean): void {
    const optionIDs = new Set(cells.filter(c => !c.isHeader && !c.isDisabled).map(c => c.optionID));
    this.timeslots.update(timeslots => (timeslots ?? []).map(t => optionIDs.has(t.optionID) ? { ...t, isChecked: checked } : t));
  }

  public dayOfWeekClicked(time: string, slots: TimeslotCell[]) {
    const filtered = slots.filter(x => x.isDisabled === false && x.isHeader === false);
    const anyTicked = filtered.find(x => x.isChecked) !== undefined;
    this.setChecked(filtered, !anyTicked);
  }

  public timeClicked(time: string) {
    const filtered = this.dtos().flat().filter(x => x.isDisabled === false && x.isHeader === false && (time === this.availabilityService.DayOfWeek || x.time === time));
    const anyTicked = filtered.find(x => x.isChecked) !== undefined;
    this.setChecked(filtered, !anyTicked);
  }

  /** Rows are rebuilt on every change; keep the row views by day. */
  public trackRow = (index: number, row: TimeslotCell[]) => row[0]?.dayOfWeek;
}
