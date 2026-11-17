import { computed, inject, Injectable, signal } from '@angular/core';
import type { ProcessRescheduleDto, ScheduleConflict, TimeSuggestion } from '@api/models';
import { RescheduleService } from '@api/services/reschedule.service';
import { SchedulingService } from '@api/services/scheduling.service';
import { errorText } from '@shared/errorText';

/** Deciding a reschedule request. One per dialog (the component provides it). */
@Injectable()
export class ProcessRescheduleDialogStore {
  private readonly api = inject(RescheduleService);
  private readonly scheduling = inject(SchedulingService);

  private readonly _processing = signal(false);
  readonly processing = this._processing.asReadonly();
  /** Why the server refused the decision; the dialog stays open to try again. */
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  /** What is wrong with the requested time (empty: nothing); null without one, or until checked. */
  private readonly _conflicts = signal<ScheduleConflict[] | null>(null);
  readonly conflicts = this._conflicts.asReadonly();
  /** The requested time can't be approved: a team already plays then, or it's outside the season. */
  readonly refused = computed(() => this._conflicts()?.some(c => c.refuses) ?? false);
  /** The best other times for the match, best first, for a decision to point to. */
  private readonly _alternatives = signal<TimeSuggestion[]>([]);
  readonly alternatives = this._alternatives.asReadonly();

  /** Checks the requested time (when there is one) against the season's rules, and loads the match's best times. */
  loadSchedule(seasonMatchID: number, requested: string | null | undefined): void {
    this._conflicts.set(null);
    this._alternatives.set([]);
    if (requested) {
      // Unchecked when it fails: the server still refuses a time that can't stand
      this.scheduling.checkMatchTime({ seasonMatchID, at: requested }).subscribe({ next: conflicts => this._conflicts.set(conflicts), error: () => this._conflicts.set(null) });
    }
    this.scheduling.getTimeSuggestions({ seasonMatchID, count: 3 }).subscribe({ next: times => this._alternatives.set(times), error: () => this._alternatives.set([]) });
  }

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
