import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { RescheduleRequestDto } from '@api/models';
import { RescheduleService } from '@api/services/reschedule.service';
import { errorText } from '@shared/errorText';

/** Asking for a match to be rescheduled. One per page (the component provides it). */
@Injectable()
export class RescheduleRequestStore {
  private readonly api = inject(RescheduleService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _submitting = signal(false);
  readonly submitting = this._submitting.asReadonly();

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
        console.error('Failed to submit reschedule request:', error);
        // The server's reason when it gave one (one already active, a match with a result, not on either team)
        this.snackBar.open(errorText(error, 'Failed to submit reschedule request. Please try again.'), 'Close', { duration: 5000 });
        this._submitting.set(false);
      },
    });
  }
}
