import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { RescheduleRequestDto, TimeSuggestion } from '@api/models';
import { RescheduleService } from '@api/services/reschedule.service';
import { SchedulingService } from '@api/services/scheduling.service';
import { errorText } from '@shared/errorText';
import { AppLog } from '@core/telemetry/appLog';

/** Asking for a match to be rescheduled. One per page (the component provides it). */
@Injectable()
export class RescheduleRequestStore {
  private readonly log = inject(AppLog);
  private readonly api = inject(RescheduleService);
  private readonly scheduling = inject(SchedulingService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _submitting = signal(false);
  readonly submitting = this._submitting.asReadonly();

  /** The match's best times from its season's timeslots, best first; null while they load, empty when there are none. */
  private readonly _suggestions = signal<TimeSuggestion[] | null>(null);
  readonly suggestions = this._suggestions.asReadonly();
  private suggestionsFor = 0;

  /** Loads the best times for the match; an earlier match's, answered late, are dropped. */
  loadSuggestions(seasonMatchID: number): void {
    this.suggestionsFor = seasonMatchID;
    this._suggestions.set(null);
    this.scheduling.getTimeSuggestions({ seasonMatchID }).subscribe({
      next: suggestions => {
        if (this.suggestionsFor === seasonMatchID) this._suggestions.set(suggestions);
      },
      // None to offer: the time can still be typed in
      error: () => {
        if (this.suggestionsFor === seasonMatchID) this._suggestions.set([]);
      },
    });
  }

  /** Sends the request; `sent` runs once the server takes it. Either way a snackbar says how it went. */
  submit(request: RescheduleRequestDto, sent: () => void): void {
    this._submitting.set(true);
    this.api.requestReschedule({ seasonMatchID: request.seasonMatchID, body: request }).subscribe({
      next: () => {
        this.snackBar.open('Reschedule request submitted successfully!', 'Close', { duration: 5000 });
        sent();
        this._submitting.set(false);
      },
      error: error => {
        this.log.error('Failed to send the reschedule request', error);
        // The server's reason when it gave one (one already active, a match with a result, not on either team)
        this.snackBar.open(errorText(error, 'Failed to submit reschedule request. Please try again.'), 'Close', { duration: 5000 });
        this._submitting.set(false);
      },
    });
  }
}
