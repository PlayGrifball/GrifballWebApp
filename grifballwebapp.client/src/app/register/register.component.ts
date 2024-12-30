import { Component, ViewChild } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ApiClientService } from '../api/apiClient.service';
import { RouterLink } from '@angular/router';
import { RegisterFormModel } from './registerFormModel';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { RegexMatchValidValidatorDirective } from '../validation/directives/regexMatchValidValidatorDirective.directive';
import { MatchFieldsValidatorDirective } from '../validation/directives/matchFieldsValidator.directive';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../account.service';

@Component({
    selector: 'app-register',
    imports: [
        MatInputModule,
        MatFormFieldModule,
        FormsModule,
        MatIconModule,
        MatButtonModule,
        RouterLink,
        ErrorMessageComponent,
        RegexMatchValidValidatorDirective,
        MatchFieldsValidatorDirective
    ],
    templateUrl: './register.component.html',
    styleUrl: './register.component.scss'
})
export class RegisterComponent {
  @ViewChild('registerForm') registerForm!: NgForm;
  
  hidePassword = true;
  hideConfirmPassword = true;

  regex: string = String.raw`^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{12,}$`;
  regexErrorMessage: string = "Minimum 12 characters, uppercase letter, lowercase letter, number and special character #?!@$%^&*-";

  model: RegisterFormModel = {} as RegisterFormModel

  constructor(private apiClient: AccountService, private snackBar: MatSnackBar) {
  }

  onSubmit() {
    if (!this.registerForm) {
      return;
    }
    if (!this.registerForm.valid) {
      return;
    }

    this.apiClient.register(this.model).subscribe(
      {
        error: (e) => {
          console.log(e);
          this.snackBar.open(`Registration failed: ${e.error}`, "Close");
        },
        next: (e) => this.snackBar.open("Registration Success", "Close"),
        //complete: () => console.log('Logged in'),
      });
  }
}
