import { Component, ChangeDetectionStrategy, computed, inject, model } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTableModule } from '@angular/material/table';
import { TimeslotDto } from '../../api/dtos/signupResponseDto';
import { AvailabilityService } from '../../availability.service';
import { FormsModule } from '@angular/forms';

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

  private buildTable(value: TimeslotDto[] | null | undefined): { timeColumns: string[], dtos: TimeslotDto[][] } {
    const timeslots = value ?? [];
    const uniqueDaysOfWeek = [...new Set(timeslots.map(item => item.dayOfWeek))];

    let timeColumns = [this.DayOfWeek];
    timeColumns.push(...[...new Set(timeslots.map(item => item.time))]);

    timeColumns = timeColumns.map(x => {
      return this.parseTime(x);
    })

    const rows: TimeslotDto[][] = [];

    // Create a new row for each day of the week
    uniqueDaysOfWeek.forEach((dayOfWeek, index, arr) => {
      const row: TimeslotDto[] = [];

      let header = new TimeslotDto();
      header.isHeader = true;
      header.dayOfWeek = dayOfWeek;
      header.time = dayOfWeek.toString();
      header.id = 0;

      // Create a new cell for each time slot, disabled if not a valid timeslot in backend
      timeColumns.forEach((time, index2, arr2) => {

        if (time === this.DayOfWeek) {
          row.push(header);
          return;
        }

        const source = timeslots.find((dto, index3, arr3) => {
          return dto.dayOfWeek === dayOfWeek && this.parseTime(dto.time) === time;
        });

        let cell: TimeslotDto;
        if (source === undefined) {
          cell = new TimeslotDto();
          cell.isDisabled = true;
          cell.dayOfWeek = dayOfWeek;
          cell.time = time;
          cell.id = 0;
          cell.isChecked = false;
          cell.isHeader = false;
        } else {
          // A copy with the display time and the values not sent from the api
          cell = { ...source, isDisabled: false, isHeader: false, time: this.parseTime(source.time) };
        }

        row.push(cell);
      });
      rows.push(row);
    });

    return { timeColumns, dtos: rows };
  }


  public getCell(time: string, slots: TimeslotDto[]): TimeslotDto {

    if (time === this.availabilityService.DayOfWeek)
      return slots[0];
    
    const c = slots.find(x => x.time === time);
    if (c !== undefined) {
      return c;
    }
    let newRow = new TimeslotDto();
    newRow.isDisabled = true;
    return newRow;
  }

  /** Sets the checked state of the timeslots behind `cells` and emits the new list. */
  public setChecked(cells: TimeslotDto[], checked: boolean): void {
    const ids = new Set(cells.filter(c => c.id != 0).map(c => c.id));
    this.timeslots.update(timeslots => (timeslots ?? []).map(t => ids.has(t.id) ? { ...t, isChecked: checked } : t));
  }

  public dayOfWeekClicked(time: string, slots: TimeslotDto[]) {
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
  public trackRow = (index: number, row: TimeslotDto[]) => row[0]?.dayOfWeek;
}
