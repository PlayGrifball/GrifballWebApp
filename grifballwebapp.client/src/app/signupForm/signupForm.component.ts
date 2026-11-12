import { Component, ChangeDetectionStrategy, computed, effect, inject, input, numberAttribute, untracked, viewChild, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import type { SignupRequestDto, SignupResponseDto } from '@api/models';
import { Observable } from 'rxjs';
import { RouterLink } from '@angular/router';
import { HomeService, SeasonService, SignupsService } from '@api/services';
import { MatSnackBar } from '@angular/material/snack-bar';
import {MatCheckboxModule} from '@angular/material/checkbox';
import { AvailabilityTableComponent } from './availabilityTable/availabilityTable.component';
import { AvailabilityService } from '../availability.service';
import { AccountService } from '../account.service';
import { eventTitles, isUnderWay, SeasonEvent } from '../shared/seasonEvents';
import { whileCurrent } from '../shared/whileCurrent';
import { errorText } from '../shared/errorText';


/** A signup with nothing filled in, for the season. */
export function emptySignup(seasonID: number): SignupRequestDto {
  return { seasonID, userID: 0, teamName: null, willCaptain: false, requiresAssistanceDrafting: false, timeslots: [] };
}
@Component({
    selector: 'app-signup-form',
    imports: [
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
        MatCheckboxModule,
        AvailabilityTableComponent,
        RouterLink,
        DatePipe,
    ],
    templateUrl: './signupForm.component.html',
    styleUrl: './signupForm.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SignupFormComponent {
  private signups = inject(SignupsService);
  private seasons = inject(SeasonService);
  private home = inject(HomeService);
  private snackBar = inject(MatSnackBar);
  private availabilityService = inject(AvailabilityService);
  accountService = inject(AccountService);

  readonly eventTitles = eventTitles;
  /** The longest team name the server takes (Team.TeamNameMaxLength): a longer one is refused, not cut. */
  readonly teamNameMaxLength = 30;
  /** The :seasonID of the route. Angular keeps this page when only it changes, so a new one loads the form for that
   * season in place of the one shown. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });
  readonly seasonName = signal<string | null>(null);
  /** When the page was opened. */
  readonly now = signal(Date.now());
  private readonly events = signal<SeasonEvent[]>([]);
  /** This season's signups, draft and regular season, in order, marking the one under way. */
  readonly timeline = computed(() => this.events()
    .filter(e => e.seasonID === this.seasonID())
    .sort((a, b) => Date.parse(a.start) - Date.parse(b.start))
    .map(e => ({ ...e, active: isUnderWay(e, this.now()) })));


  readonly signupForm = viewChild.required<NgForm>('signupForm');
  // Initialised with an empty signup (timeslots: []) because the template binds
  // model.timeslots on the first render, before getSignup has responded.
  readonly model = signal(emptySignup(0));
  /** The signup and its timeslots are in (or failed to load): the form is shown then, in one go,
   * so the availability table does not push the rest of the page down as it arrives. */
  readonly loaded = signal(false);

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      untracked(() => this.load(seasonID));
    });
  }

  private load(seasonID: number): void {
    // Nothing of the previous season's signup stays in the form
    this.model.set(emptySignup(0));
    this.seasonName.set(null);
    this.loaded.set(false);

    // Fetch from backend
    if (seasonID > 0) {
      this.loadSeasonInfo(seasonID);
      // null before the user signs up (not a 404: the season takes signups, this user has none yet).
      (this.signups.getSignup({ seasonID, offset: this.availabilityService.getDif() }) as Observable<SignupResponseDto | null>)
        .pipe(whileCurrent(seasonID, this.seasonID))
        .subscribe({
          next: (result) =>
          {
            if (result === null)
            {
              this.model.set(emptySignup(seasonID));
              this.signups.getTimeslots({ seasonID, offset: this.availabilityService.getDif() }).pipe(whileCurrent(seasonID, this.seasonID)).subscribe({
                next: (r) => {
                  this.model.update(model => ({ ...model, timeslots: r }));
                  this.loaded.set(true);
                },
                error: () => {
                  this.snackBar.open('Failed to get timeslots');
                  this.loaded.set(true);
                },
              })
            }
            else
            {
              this.model.set({ ...result, timeslots: result.timeslots ?? [] });
              this.loaded.set(true);
            }
          },
          error: () => {
            this.snackBar.open('Failed to get signup');
            this.loaded.set(true);
          },
        });
      return;
    }
    this.loaded.set(true);

  }

  /** The season's name and dates, for the heading and the timeline. Neither is needed to sign up. */
  private loadSeasonInfo(seasonID: number): void {
    this.seasons.getSeasonName({ seasonID }).pipe(whileCurrent(seasonID, this.seasonID)).subscribe({ next: name => this.seasonName.set(name), error: () => {} });
    this.home.currentAndFutureEvents().subscribe({ next: events => this.events.set(events), error: () => {} });
  }

  /** Every edit (form fields and the availability table) replaces the model rather than mutating it. */
  patch(changes: Partial<SignupRequestDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  onSubmit() {
    if (!this.signupForm().valid) {
      return;
    }

    const dto = this.model();
    this.signups.upsertSignup({ seasonID: dto.seasonID, body: dto }).subscribe({
      next: (result) => this.snackBar.open('Signup Submitted', 'OK'),
      error: (result: HttpErrorResponse) =>
      {
        console.log(result);
        this.snackBar.open('Error Occurred: ' + errorText(result, 'The signup could not be saved'), 'OK');
      },
      });
  }
}
