import { Component, ChangeDetectionStrategy, computed, inject, input, linkedSignal, numberAttribute, viewChild, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import type { SignupBodyDto } from '@api/models';
import { RouterLink } from '@angular/router';
import {MatCheckboxModule} from '@angular/material/checkbox';
import { AvailabilityTableComponent } from './availabilityTable/availabilityTable.component';
import { AccountService } from '@core/auth/account.service';
import { eventTitles, isUnderWay } from '@shared/seasonEvents';
import { SignupFormStore } from './signupForm.store';

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
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [SignupFormStore],
})
export class SignupFormComponent {
  private readonly store = inject(SignupFormStore);
  accountService = inject(AccountService);

  readonly eventTitles = eventTitles;
  /** The longest team name the server takes (Team.TeamNameMaxLength): a longer one is refused, not cut. */
  readonly teamNameMaxLength = 30;
  /** The :seasonID of the route. Angular keeps this page when only it changes, so a new one loads the form for that
   * season in place of the one shown. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });
  readonly seasonName = this.store.seasonName;
  /** When the page was opened. */
  readonly now = signal(Date.now());
  private readonly events = this.store.events;
  /** This season's signups, draft and regular season, in order, marking the one under way. */
  readonly timeline = computed(() => this.events()
    .filter(e => e.seasonID === this.seasonID())
    .sort((a, b) => Date.parse(a.start) - Date.parse(b.start))
    .map(e => ({ ...e, active: isUnderWay(e, this.now()) })));


  readonly signupForm = viewChild.required<NgForm>('signupForm');
  /** The form: the season's signup as it loads (empty until then); edits replace it. */
  readonly model = linkedSignal(() => this.store.signup());
  /** The signup and its timeslots are in (or failed to load): the form is shown then, in one go,
   * so the availability table does not push the rest of the page down as it arrives. */
  readonly loaded = this.store.loaded;

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }

  /** Every edit (form fields and the availability table) replaces the model rather than mutating it. */
  patch(changes: Partial<SignupBodyDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  onSubmit() {
    if (!this.signupForm().valid) {
      return;
    }

    // The signed-in player's own signup, for this page's season
    this.store.submit(this.seasonID(), this.model());
  }
}
