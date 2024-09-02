import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTableModule } from '@angular/material/table';
import { TimeslotDto } from '../../api/dtos/signupResponseDto';
import { AvailabilityService } from '../../availability.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-availability-table',
  standalone: true,
  imports: [
    CommonModule,
    MatCheckboxModule,
    MatTableModule,
    FormsModule,
  ],
  templateUrl: './availabilityTable.component.html',
  styleUrl: './availabilityTable.component.scss',
})
export class AvailabilityTableComponent {

  DayOfWeek: string = this.availabilityService.DayOfWeek;

  private parseTime(time: string): string {
    return this.availabilityService.parseTime(time);
  }

  constructor(private availabilityService: AvailabilityService) { }

  @Input({ required: true })
  public set timeslots(timeslots: TimeslotDto[]) {
    const uniqueDaysOfWeek = [...new Set(timeslots.map(item => item.dayOfWeek))];

    this.timeColumns = [this.DayOfWeek];
    this.timeColumns.push(...[...new Set(timeslots.map(item => item.time))]);

    this.timeColumns = this.timeColumns.map(x => {
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
      this.timeColumns.forEach((time, index2, arr2) => {

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

    this.dtos = rows;
  }
  public get timeslots(): TimeslotDto[] {
    return this.dtos.flat().filter(x => x.id != 0);
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
    const filtered = this.dtos.flat().filter(x => x.isDisabled === false && x.isHeader === false && (time === this.availabilityService.DayOfWeek || x.time === time));
    const anyTicked = filtered.find(x => x.isChecked) !== undefined;
    if (anyTicked) {
      filtered.forEach(x => x.isChecked = false);
    } else {
      filtered.forEach(x => x.isChecked = true);
    }
  }

  public timeColumns: string[] = [];
  public dtos: TimeslotDto[][] = [];
}
