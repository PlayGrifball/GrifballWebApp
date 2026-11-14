import { Component, OnInit, ChangeDetectionStrategy, inject, input, linkedSignal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormField, MatInputModule } from '@angular/material/input';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { DateTime } from 'luxon';
import { MatSelectModule } from '@angular/material/select';
import { provideDatePickers } from '@shared/datePickers';
import { RouterLink } from '@angular/router';
import type { DayOfWeek } from '@api/models';
import { SeasonAvailabilityStore } from './seasonAvailability.store';


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
    providers: [provideDatePickers(), SeasonAvailabilityStore],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonAvailabilityComponent implements OnInit {
  private readonly store = inject(SeasonAvailabilityStore);

  ngOnInit(): void {
    this.store.load(this.seasonID());
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

  /** The rows being edited: the season's timeslots once they arrive. */
  readonly options = linkedSignal<AvailabilityOption[]>(() => this.store.timeslots());

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
    // A row whose day was never picked goes as it is, and the server refuses the save, as it always has
    this.store.save(this.seasonID(), this.options().map(o => ({ dayOfWeek: o.dayOfWeek as DayOfWeek, time: o.time })));
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
}

export interface DayOfWeekCheckbox {
  name: DayOfWeek;
  isChecked: boolean;
}
