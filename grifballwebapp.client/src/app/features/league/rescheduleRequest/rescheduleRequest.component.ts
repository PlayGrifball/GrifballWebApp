import { Component, ChangeDetectionStrategy, effect, inject, input, numberAttribute, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Location } from '@angular/common';
import type { RescheduleRequestDto } from '@api/models';
import { RescheduleRequestStore } from './rescheduleRequest.store';

@Component({
  selector: 'app-reschedule-request',
  standalone: true,
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSnackBarModule
  ],
  templateUrl: './rescheduleRequest.component.html',
  styleUrls: ['./rescheduleRequest.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [RescheduleRequestStore],
})
export class RescheduleRequestComponent {
  location = inject(Location);
  private readonly store = inject(RescheduleRequestStore);
  private snackBar = inject(MatSnackBar);

  /** The :seasonMatchID of the route. */
  readonly seasonMatchID = input.required({ transform: numberAttribute });
  newDate?: Date;
  newTime: string = '';
  reason: string = '';
  readonly submitting = this.store.submitting;

  constructor() {
    // Angular keeps this page when only the match in its route changes: what was filled in for the previous match
    // is cleared.
    effect(() => {
      this.seasonMatchID();
      untracked(() => {
        this.newDate = undefined;
        this.newTime = '';
        this.reason = '';
      });
    });
  }

  submitRescheduleRequest(): void {
    if (!this.reason.trim()) {
      this.snackBar.open('Please provide a reason for the reschedule', 'Close', { duration: 3000 });
      return;
    }

    let newScheduledTime: string | null = null;
    if (this.newDate && this.newTime) {
      const [hours, minutes] = this.newTime.split(':').map(Number);
      const dateTime = new Date(this.newDate);
      dateTime.setHours(hours, minutes, 0, 0);
      newScheduledTime = dateTime.toISOString();
    }

    const request: RescheduleRequestDto = {
      seasonMatchID: this.seasonMatchID(),
      newScheduledTime,
      reason: this.reason.trim()
    };

    this.store.submit(request, () => this.location.back());
  }
}