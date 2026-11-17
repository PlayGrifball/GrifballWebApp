import { Component, ChangeDetectionStrategy, effect, inject, input, numberAttribute, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Location } from '@angular/common';
import type { RescheduleRequestDto, TimeSuggestion } from '@api/models';
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
  /** The match's best times, best first (null while they load): picking one fills in the date and time. */
  readonly suggestions = this.store.suggestions;

  constructor() {
    // Angular keeps this page when only the match in its route changes: what was filled in for the previous match
    // is cleared, and its best times loaded.
    effect(() => {
      const seasonMatchID = this.seasonMatchID();
      untracked(() => {
        this.store.loadSuggestions(seasonMatchID);
        this.newDate = undefined;
        this.newTime = '';
        this.reason = '';
      });
    });
  }

  /** Fills in the date and time (in the browser's time zone, as they're typed) from a suggested one. */
  pick(suggestion: TimeSuggestion): void {
    const at = new Date(suggestion.startsAt);
    this.newDate = new Date(at.getFullYear(), at.getMonth(), at.getDate());
    this.newTime = `${String(at.getHours()).padStart(2, '0')}:${String(at.getMinutes()).padStart(2, '0')}`;
  }

  formatTime(startsAt: string): string {
    return new Date(startsAt).toLocaleString();
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