import { Component, OnInit, ChangeDetectionStrategy, inject, input, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormField, MatInputModule } from '@angular/material/input';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { DateTime } from 'luxon';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideDatePickers } from '../../../../shared/datePickers';
import { RouterLink } from '@angular/router';
import { AvailabilityService } from '@api/services/availability.service';
import type { AvailabilityTimeslotDto, DayOfWeek, SeasonAvailabilityDto } from '@api/models';


/** A row of the editor: a timeslot, its day empty until one is picked. */
export class AvailabilityOption {
  dayOfWeek: DayOfWeek | '' = '';
  time: string = '';
}

@Component({
    selector: 'app-season-availability',
    imports: [
        MatCheckboxModule,
        MtxDatetimepickerModule,
        MatInputModule,
        FormsModule,
        MatFormField,
        MatButton,
        MatSelectModule,
        RouterLink,
    ],
    templateUrl: './seasonAvailability.component.html',
    styleUrl: './seasonAvailability.component.scss',
    providers: [provideDatePickers()],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonAvailabilityComponent implements OnInit {
  private availability = inject(AvailabilityService);
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

  /** Row edits replace the option (and the list) rather than mutating them. */
  setOption(index: number, changes: Partial<AvailabilityOption>): void {
    this.options.update(options => options.map((o, i) => i === index ? { ...o, ...changes } : o));
  }

  add(): void {
    this.options.update(options => [new AvailabilityOption(), ...options]);
  }

  delete(index: number): void {
    this.options.update(options => options.filter((_, i) => i !== index));
  }

  updateSeasonAvailability() {
    const body: SeasonAvailabilityDto = {
      seasonID: this.seasonID(),
      // A row whose day was never picked goes as it is, and the server refuses the save, as it always has
      timeslots: this.options().map(o => ({ dayOfWeek: o.dayOfWeek as DayOfWeek, time: o.time })),
    };
    return this.availability.updateSeasonAvailability({ seasonID: this.seasonID(), body });
  }

  getSeasonAvailability(): Observable<AvailabilityTimeslotDto[]> {
    return this.availability.getSeasonAvailability({ seasonID: this.seasonID() });
  }
}

export interface DayOfWeekCheckbox {
  name: DayOfWeek;
  isChecked: boolean;
}
