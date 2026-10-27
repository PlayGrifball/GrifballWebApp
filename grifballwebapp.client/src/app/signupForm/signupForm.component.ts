import { Component, ChangeDetectionStrategy, inject, viewChild, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { SignupRequestDto } from '../api/dtos/signupRequestDto';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import {MatCheckboxModule} from '@angular/material/checkbox';
import { AvailabilityTableComponent } from './availabilityTable/availabilityTable.component';
import { AvailabilityService } from '../availability.service';

@Component({
    selector: 'app-signup-form',
    imports: [
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
        MatCheckboxModule,
        AvailabilityTableComponent
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


  readonly signupForm = viewChild.required<NgForm>('signupForm');
  // Initialised with the class defaults (timeslots: []) because the template binds
  // model.timeslots on the first render, before getSignup has responded.
  readonly model = signal(new SignupRequestDto());

  ngOnInit(): void {
    // May need smart way to get seasonID
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    // Fetch from backend
    if (seasonID > 0) {
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
                next: (r) => this.model.update(model => ({ ...model, timeslots: r })),
                error: () => this.snackBar.open('Failed to get timeslots'),
              })
            }
            else
            {
              this.model.set({ ...result, timeslots: result.timeslots ?? [] });
            }
          },
          error: () => this.snackBar.open('Failed to get signup'),
        });
      return;
    }

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
