import { HttpErrorResponse } from '@angular/common/http';
import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { SignupBodyDto, SignupResponseDto } from '@api/models';
import { HomeService } from '@api/services/home.service';
import { SeasonService } from '@api/services/season.service';
import { SignupsService } from '@api/services/signups.service';
import { noContentAsNull } from '@core/http/noContentAsNull';
import { errorText } from '@shared/errorText';
import { SeasonEvent } from '@shared/seasonEvents';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import { AvailabilityService } from '../availability.service';

/** A signup with nothing filled in. */
export function emptySignup(): SignupBodyDto {
  return { teamName: null, willCaptain: false, requiresAssistanceDrafting: false, timeslots: [] };
}

/** What the form edits of a player's signup. */
function signupBody(signup: SignupResponseDto): SignupBodyDto {
  // An answer without its timeslots still gives the availability table a list
  return { teamName: signup.teamName, willCaptain: signup.willCaptain, requiresAssistanceDrafting: signup.requiresAssistanceDrafting, timeslots: signup.timeslots ?? [] };
}

/**
 * The signed-in player's signup for a season: theirs, or an empty one with the season's timeslots, the season's name
 * and dates, and saving it. One per page (the component provides it).
 */
@Injectable()
export class SignupFormStore {
  private readonly signups = inject(SignupsService);
  private readonly seasons = inject(SeasonService);
  private readonly home = inject(HomeService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly availabilityService = inject(AvailabilityService);

  /** The route's season: the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** The signup as loaded: the player's, or an empty one with the season's timeslots; empty until they arrive. */
  private readonly _signup = signal(emptySignup());
  readonly signup = this._signup.asReadonly();
  private readonly _seasonName = signal<string | null>(null);
  readonly seasonName = this._seasonName.asReadonly();
  private readonly _events = signal<SeasonEvent[]>([]);
  readonly events = this._events.asReadonly();
  /** The signup and its timeslots are in (or failed to load). */
  private readonly _loaded = signal(false);
  readonly loaded = this._loaded.asReadonly();

  constructor() {
    effect(() => {
      const seasonID = this.seasonID.value();
      if (seasonID !== undefined)
        untracked(() => this.load(seasonID));
    });
  }

  /** Saves the player's signup for the season, and says how it went. */
  submit(seasonID: number, body: SignupBodyDto): void {
    this.signups.upsertSignup({ seasonID, body }).subscribe({
      next: () => this.snackBar.open('Signup Submitted', 'OK'),
      error: (result: HttpErrorResponse) => {
        console.log(result);
        this.snackBar.open('Error Occurred: ' + errorText(result, 'The signup could not be saved'), 'OK');
      },
    });
  }

  private load(seasonID: number): void {
    // Nothing of the previous season's signup stays
    this._signup.set(emptySignup());
    this._seasonName.set(null);
    this._loaded.set(false);
    if (seasonID <= 0) {
      this._loaded.set(true);
      return;
    }
    this.loadSeasonInfo(seasonID);
    const offset = this.availabilityService.getDif();
    // 204 before the user signs up (not a 404: the season takes signups, this user has none yet).
    this.signups.getSignup({ seasonID, offset })
      .pipe(noContentAsNull(), whileCurrent(seasonID, this.seasonID.value))
      .subscribe({
        next: result => {
          if (result !== null) {
            this._signup.set(signupBody(result));
            this._loaded.set(true);
            return;
          }
          this.signups.getTimeslots({ seasonID, offset }).pipe(whileCurrent(seasonID, this.seasonID.value)).subscribe({
            next: timeslots => {
              this._signup.set({ ...emptySignup(), timeslots });
              this._loaded.set(true);
            },
            error: () => {
              this.snackBar.open('Failed to get timeslots');
              this._loaded.set(true);
            },
          });
        },
        error: () => {
          this.snackBar.open('Failed to get signup');
          this._loaded.set(true);
        },
      });
  }

  /** The season's name and dates, for the heading and the timeline. Neither is needed to sign up. */
  private loadSeasonInfo(seasonID: number): void {
    this.seasons.getSeasonName({ seasonID }).pipe(whileCurrent(seasonID, this.seasonID.value)).subscribe({ next: name => this._seasonName.set(name), error: () => {} });
    this.home.currentAndFutureEvents().subscribe({ next: events => this._events.set(events), error: () => {} });
  }
}
