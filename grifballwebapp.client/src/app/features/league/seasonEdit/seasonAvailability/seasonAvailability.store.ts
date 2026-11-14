import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { AvailabilityTimeslotDto } from '@api/models';
import { AvailabilityService } from '@api/services/availability.service';

/** A season's availability timeslots: loading them and saving the edited ones. One per page (the component provides it). */
@Injectable()
export class SeasonAvailabilityStore {
  private readonly availability = inject(AvailabilityService);
  private readonly snackbar = inject(MatSnackBar);

  /** The timeslots as loaded; none until they are, or when they could not be. */
  private readonly _timeslots = signal<AvailabilityTimeslotDto[]>([]);
  readonly timeslots = this._timeslots.asReadonly();

  load(seasonID: number): void {
    this.availability.getSeasonAvailability({ seasonID }).subscribe({
      next: timeslots => this._timeslots.set(timeslots),
      error: () => this.snackbar.open('Failed to get season availability'),
    });
  }

  /** Saves the season's timeslots, and says how it went. */
  save(seasonID: number, timeslots: AvailabilityTimeslotDto[]): void {
    this.availability.updateSeasonAvailability({ seasonID, body: { seasonID, timeslots } }).subscribe({
      next: () => this.snackbar.open('Saved'),
      error: () => this.snackbar.open('Failed to save'),
    });
  }
}
