import { Component, OnInit, ChangeDetectionStrategy, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormField, MatInputModule } from '@angular/material/input';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { DateTime } from 'luxon';
import { AvailabilityOption } from '../../api/dtos/availabilityOption';
import { MatSelect, MatSelectModule } from '@angular/material/select';
import { HttpClient } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
    selector: 'app-season-availability',
    imports: [
        MatCheckboxModule,
        MtxDatetimepickerModule,
        MatInputModule,
        FormsModule,
        MatFormField,
        MatButton,
        MatSelectModule
    ],
    templateUrl: './seasonAvailability.component.html',
    styleUrl: './seasonAvailability.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonAvailabilityComponent implements OnInit {
  private http = inject(HttpClient);
  private snackbar = inject(MatSnackBar);


  ngOnInit(): void {
    this.getSeasonAvailability()
    .subscribe({
      next: x => this.options.set(x),
      error: () => this.snackbar.open('Failed to get season availability'),
    });
  }

  readonly seasonID = input.required<number>();

  minutes: number = 30;

  start: DateTime = DateTime.fromFormat('19:00', 'hh:mm');
  end: DateTime = DateTime.fromFormat('22:00', 'hh:mm');

  checkboxes: DayOfWeekCheckbox[] = [
    {
      name: 'Sunday',
      isChecked: true,
    },
    {
      name: 'Monday',
      isChecked: true,
    },
    {
      name: 'Tuesday',
      isChecked: true,
    },
    {
      name: 'Wednesday',
      isChecked: true,
    },
    {
      name: 'Thursday',
      isChecked: true,
    },
    {
      name: 'Friday',
      isChecked: true,
    },
    {
      name: 'Saturday',
      isChecked: true,
    },
  ]

  daysOfWeek(): string[] {
    return this.checkboxes.map(x => x.name);
  }

  readonly options = signal<AvailabilityOption[]>([]);

  bulkModify(): void {
    const options: AvailabilityOption[] = [];

    let time = this.start;

    while (time <= this.end) {
      for (const dayOfWeek of this.checkboxes.filter(x => x.isChecked).map(x => x.name)) {
        const o = new AvailabilityOption();
        o.dayOfWeek = dayOfWeek;
        o.time = time.toString();

        options.push(o);
      }

      time = time.plus({ minutes: this.minutes });
    }

    this.options.set(options);
  }

  saveChanges(): void {
    this.updateSeasonAvailability().subscribe({
      next: () => this.snackbar.open('Saved'),
      error: () => this.snackbar.open('Failed to save'),
    });
  }

  add(): void {
    this.options.update(options => [new AvailabilityOption(), ...options]);
  }

  delete(index: number): void {
    this.options.update(options => options.filter((_, i) => i !== index));
  }

  updateSeasonAvailability() {
    return this.http.post('api/Availability/UpdateSeasonAvailability', {
      seasonID: this.seasonID(),
      timeslots: this.options(),
    });
  }

  getSeasonAvailability() {
    return this.http.get<AvailabilityOption[]>('api/Availability/GetSeasonAvailability?seasonID=' + this.seasonID());
  }
}

export interface DayOfWeekCheckbox {
  name: string;
  isChecked: boolean;
}
