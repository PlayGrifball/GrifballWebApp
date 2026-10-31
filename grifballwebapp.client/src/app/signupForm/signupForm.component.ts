import { Component, ChangeDetectionStrategy, computed, inject, viewChild, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { SignupRequestDto } from '../api/dtos/signupRequestDto';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import {MatCheckboxModule} from '@angular/material/checkbox';
import { AvailabilityTableComponent } from './availabilityTable/availabilityTable.component';
import { AvailabilityService } from '../availability.service';
import { AccountService } from '../account.service';
import { eventTitles, isUnderWay, SeasonEvent } from '../shared/seasonEvents';

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
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);
  private snackBar = inject(MatSnackBar);
  private availabilityService = inject(AvailabilityService);
  private http = inject(HttpClient);
  accountService = inject(AccountService);

  readonly eventTitles = eventTitles;
  /** The longest team name the server takes (Team.TeamNameMaxLength): a longer one is refused, not cut. */
  readonly teamNameMaxLength = 30;
  readonly seasonID = signal(0);
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
  // Initialised with the class defaults (timeslots: []) because the template binds
  // model.timeslots on the first render, before getSignup has responded.
  readonly model = signal(new SignupRequestDto());
  /** The signup and its timeslots are in (or failed to load): the form is shown then, in one go,
   * so the availability table does not push the rest of the page down as it arrives. */
  readonly loaded = signal(false);

  ngOnInit(): void {
    // May need smart way to get seasonID
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));
    this.seasonID.set(seasonID);

    // Fetch from backend
    if (seasonID > 0) {
      this.loadSeasonInfo(seasonID);
      this.api.getSignup(seasonID, this.availabilityService.getDif())
        .subscribe({
          next: (result) =>
          {
            if (result === null)
            {
              const model = new SignupRequestDto();
              model.seasonID = seasonID;
              this.model.set(model);
              this.api.getTimeslots(seasonID, this.availabilityService.getDif()).subscribe({
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
    this.api.getSeasonName(seasonID).subscribe({ next: name => this.seasonName.set(name), error: () => {} });
    this.http.get<SeasonEvent[]>('/api/Home/CurrentAndFutureEvents/').subscribe({ next: events => this.events.set(events), error: () => {} });
  }

  /** Every edit (form fields and the availability table) replaces the model rather than mutating it. */
  patch(changes: Partial<SignupRequestDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  onSubmit() {
    if (!this.signupForm().valid) {
      return;
    }

    this.api.upsertSignup(this.model()).subscribe({
      next: (result) => this.snackBar.open('Signup Submitted', 'OK'),
      error: (result) =>
      {
        console.log(result);
        this.snackBar.open('Error Occurred: ' + result.error, 'OK');
      },
      });
  }
}
