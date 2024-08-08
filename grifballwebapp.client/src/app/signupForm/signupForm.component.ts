import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, ViewChild } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { SignupRequestDto } from '../api/dtos/signupRequestDto';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import {MatCheckboxModule} from '@angular/material/checkbox';
import { DateTime } from 'luxon';
import { TimeslotDto } from '../api/dtos/signupResponseDto';
import { MatTableModule } from '@angular/material/table';
import { head } from 'lodash';

@Component({
  selector: 'app-signup-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    MatCheckboxModule,
    MatTableModule,
  ],
  templateUrl: './signupForm.component.html',
  styleUrl: './signupForm.component.scss',
})
export class SignupFormComponent {

  DayOfWeek: string = 'Day Of Week';

  @ViewChild('signupForm') registerForm!: NgForm;
  model: SignupRequestDto = {} as SignupRequestDto;

  constructor(private route: ActivatedRoute, private api: ApiClientService, private snackBar: MatSnackBar) { }

  getDif(): number {
    const detroitOffset = DateTime.now().setZone('America/Detroit').offset;
    const localOffset = DateTime.now().offset;
    const dif = localOffset - detroitOffset;
    return dif;
  }

  private parseTime(time: string): string {
    if (time === this.DayOfWeek)
      return time;
    let dateTime = DateTime.fromFormat(time, 'TT');
    return dateTime.toFormat('t');
  }

  ngOnInit(): void {
    // May need smart way to get seasonID
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    // Fetch from backend
    if (seasonID > 0) {
      this.api.getSignup(seasonID, null, this.getDif())
        .subscribe({
          next: (result) =>
          {
            if (result === null)
            {
              this.model = {} as SignupRequestDto;
              this.model.seasonID = seasonID;
            }
            else
            {
              this.model = result;

              const uniqueDaysOfWeek = [...new Set(result.timeslots.map(item => item.dayOfWeek))];

              this.timeColumns = [this.DayOfWeek];
              this.timeColumns.push(...[...new Set(result.timeslots.map(item => item.time))]);

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

                  let data = result.timeslots.find((dto, index3, arr3) => {
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
          },
        });
      return;
    }

  }

  onSubmit() {
    if (!this.registerForm.valid) {
      return;
    }

    this.api.upsertSignup(this.model).subscribe({
      next: (result) => this.snackBar.open('Signup Submitted', 'OK'),
      error: (result) =>
      {
        console.log(result);
        this.snackBar.open('Error Occurred: ' + result.error, 'OK');
      },
      });
  }

  public getCell(time: string, slots: TimeslotDto[]): TimeslotDto {

    if (time === this.DayOfWeek)
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
    const filtered = this.dtos.flat().filter(x => x.isDisabled === false && x.isHeader === false && (time === this.DayOfWeek || x.time === time));
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
