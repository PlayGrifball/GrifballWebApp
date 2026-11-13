import { Component, ChangeDetectionStrategy, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Location } from '@angular/common';
import { errorText } from '../../../shared/errorText';
import { RescheduleService } from '@api/services/reschedule.service';
import type { RescheduleRequestDto } from '@api/models';

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
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RescheduleRequestComponent {
  location = inject(Location);
  private rescheduleService = inject(RescheduleService);
  private snackBar = inject(MatSnackBar);

  /** The :seasonMatchID of the route. */
  readonly seasonMatchID = input.required({ transform: numberAttribute });
  newDate?: Date;
  newTime: string = '';
  reason: string = '';
  readonly submitting = signal(false);

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

    this.submitting.set(true);

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

    this.rescheduleService.requestReschedule({ seasonMatchID: request.seasonMatchID, body: request })
    .subscribe({
      next: () => {
        this.snackBar.open('Reschedule request submitted successfully!', 'Close', { duration: 5000 });
        this.location.back();
        this.submitting.set(false);
      },
      error: (error) => {
        console.error('Failed to submit reschedule request:', error);
        // The server's reason when it gave one (one already active, a match with a result, not on either team)
        this.snackBar.open(errorText(error, 'Failed to submit reschedule request. Please try again.'), 'Close', { duration: 5000 });
        this.submitting.set(false);
      }
    });
  }
}