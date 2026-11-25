import { inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { AvailabilityTimeslotDto } from '@api/models';
import { AvailabilityService } from '@api/services/availability.service';
import { resourceView } from '@shared/resourceView';
import { StoreInput } from '@shared/storeInput';
import { tap } from 'rxjs';

/** A season's availability timeslots: loading them and saving the edited ones. One per page (the component provides it). */
@Injectable()
export class SeasonAvailabilityStore {
  private readonly availability = inject(AvailabilityService);
  private readonly snackbar = inject(MatSnackBar);

  /** The season: the component's input. */
  readonly seasonID = new StoreInput<number>();

  private readonly loaded = resourceView(rxResource({
    params: () => this.seasonID.value(),
    stream: ({ params: seasonID }) => this.availability.getSeasonAvailability({ seasonID })
      .pipe(tap({ error: () => this.snackbar.open('Failed to get season availability') })),
  }), []);
  /** The timeslots as loaded; none until they are, or when they could not be. */
  readonly timeslots = this.loaded.value;
  /** The timeslots are on their way: the list is a skeleton. */
  readonly firstLoad = this.loaded.firstLoad;
  /** They could not be read (the snackbar says so). */
  readonly failed = this.loaded.failed;

  /** Saves the season's timeslots, and says how it went. */
  save(seasonID: number, timeslots: AvailabilityTimeslotDto[]): void {
    this.availability.updateSeasonAvailability({ seasonID, body: { seasonID, timeslots } }).subscribe({
      next: () => this.snackbar.open('Saved'),
      error: () => this.snackbar.open('Failed to save'),
    });
  }
}
