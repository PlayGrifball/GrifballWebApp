import { inject, Injectable, signal } from '@angular/core';
import type { ProcessRescheduleDto } from '@api/models';
import { RescheduleService } from '@api/services/reschedule.service';
import { errorText } from '@shared/errorText';

/** Deciding a reschedule request. One per dialog (the component provides it). */
@Injectable()
export class ProcessRescheduleDialogStore {
  private readonly api = inject(RescheduleService);

  private readonly _processing = signal(false);
  readonly processing = this._processing.asReadonly();
  /** Why the server refused the decision; the dialog stays open to try again. */
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  /** Sends the decision; `done` runs once the server takes it. */
  process(rescheduleID: number, decision: ProcessRescheduleDto, done: () => void): void {
    this._processing.set(true);
    this._error.set(null);
    this.api.processReschedule({ rescheduleID, body: decision }).subscribe({
      next: () => {
        done();
        this._processing.set(false);
      },
      error: error => {
        // The server says why (e.g. "This match already has a result."); otherwise a general message
        this._error.set(errorText(error, 'The request could not be processed. Try again.'));
        this._processing.set(false);
      },
    });
  }
}
