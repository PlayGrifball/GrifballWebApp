import { Component, ChangeDetectionStrategy, computed, inject, input } from '@angular/core';
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
  readonly timeslots = input.required<TimeslotDto[] | null | undefined>();

  private readonly table = computed(() => this.buildTable(this.timeslots()));
  readonly timeColumns = computed(() => this.table().timeColumns);
  readonly dtos = computed(() => this.table().dtos);

  /** Every real timeslot in the grid, leaving out the day-of-week headers and filler cells. */
  selectableTimeslots(): TimeslotDto[] {
    return this.dtos().flat().filter(x => x.id != 0);
  }

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

        let data = timeslots.find((dto, index3, arr3) => {
          return dto.dayOfWeek === dayOfWeek && this.parseTime(dto.time) === time;
        });

        if (data === undefined) {
          data = new TimeslotDto();
          data.isDisabled = true;
          data.dayOfWeek = dayOfWeek;
          data.time = time;
          data.id = 0;
          data.isChecked = false;
          data.isHeader = false;
        } else {
          // Set values not sent from api
          data.isDisabled = false;
          data.isHeader = false;
          // Parse time
          data.time = this.parseTime(data.time);
        }

        row.push(data);
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

  public dayOfWeekClicked(time: string, slots: TimeslotDto[]) {
    const filtered = slots.filter(x => x.isDisabled === false && x.isHeader === false);
    const anyTicked = filtered.find(x => x.isChecked) !== undefined;
    if (anyTicked) {
      filtered.forEach(x => x.isChecked = false);
    } else {
      filtered.forEach(x => x.isChecked = true);
    }
  }

  public timeClicked(time: string) {
    const filtered = this.dtos().flat().filter(x => x.isDisabled === false && x.isHeader === false && (time === this.availabilityService.DayOfWeek || x.time === time));
    const anyTicked = filtered.find(x => x.isChecked) !== undefined;
    if (anyTicked) {
      filtered.forEach(x => x.isChecked = false);
    } else {
      filtered.forEach(x => x.isChecked = true);
    }
  }

}
