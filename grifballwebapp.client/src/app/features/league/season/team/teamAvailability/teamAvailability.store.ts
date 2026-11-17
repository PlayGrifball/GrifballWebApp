import { inject, Injectable, signal } from '@angular/core';
import type { TeamSlots } from '@api/models';
import { SchedulingService } from '@api/services/scheduling.service';
import { errorText } from '@shared/errorText';

/** A team's timeslots, as scheduling sees them: its roster's, unless its captain set its own. One per editor (the component provides it). */
@Injectable()
export class TeamAvailabilityStore {
  private readonly api = inject(SchedulingService);

  private readonly _slots = signal<TeamSlots | null>(null);
  /** The team's timeslots; null until loaded. */
  readonly slots = this._slots.asReadonly();
  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  /** Why the server refused a change. */
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  load(teamID: number): void {
    this.api.getTeamAvailability({ teamID }).subscribe({ next: slots => this._slots.set(slots), error: () => this._slots.set(null) });
  }

  /** The captain (or staff) set the team's own timeslots, in place of its roster's. */
  save(teamID: number, availabilityOptionIDs: number[]): void {
    this.change(teamID, this.api.setTeamAvailability({ teamID, body: { availabilityOptionIDs } }));
  }

  /** The captain (or staff) reset the team's timeslots to its roster's. */
  reset(teamID: number): void {
    this.change(teamID, this.api.resetTeamAvailability({ teamID }));
  }

  private change(teamID: number, request: ReturnType<SchedulingService['resetTeamAvailability']>): void {
    this._saving.set(true);
    this._error.set(null);
    request.subscribe({
      next: () => {
        this._saving.set(false);
        this.load(teamID);
      },
      error: error => {
        this._error.set(errorText(error, "The team's availability could not be changed. Try again."));
        this._saving.set(false);
      },
    });
  }
}
