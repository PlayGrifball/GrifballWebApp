import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, ViewChild } from '@angular/core';
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

@Component({
  selector: 'app-signup-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    MatCheckboxModule
  ],
  templateUrl: './signupForm.component.html',
  styleUrl: './signupForm.component.scss',
})
export class SignupFormComponent {
  @ViewChild('signupForm') registerForm!: NgForm;
  model: SignupRequestDto = {} as SignupRequestDto;

  constructor(private route: ActivatedRoute, private api: ApiClientService, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    // May need smart way to get seasonID
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    // Fetch from backend
    if (seasonID > 0) {
      this.api.getSignup(seasonID, null)
        .subscribe({
          next: (result) =>
          {
            if (result === null)
            {
              this.model = {} as SignupRequestDto;
              this.model.seasonID = seasonID;
            }
            else
            {
              this.model = result;
            }
            
          },
        });
      return;
    }

  }

  onSubmit() {
    if (!this.registerForm.valid) {
      return;
    }

    this.api.upsertSignup(this.model).subscribe({
      next: (result) => this.snackBar.open('Signup Submitted', 'OK'),
      error: (result) =>
      {
        console.log(result);
        this.snackBar.open('Error Occurred: ' + result.error, 'OK');
      },
      });
  }
}
