import { Component, viewChild, ChangeDetectionStrategy, inject } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { RegisterFormModel } from './registerFormModel';
import { ErrorMessageComponent } from '../../../shared/validation/errorMessage.component';
import { RegexMatchValidValidatorDirective } from '../../../shared/validation/directives/regexMatchValidValidatorDirective.directive';
import { MatchFieldsValidatorDirective } from '../../../shared/validation/directives/matchFieldsValidator.directive';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../../../core/auth/account.service';
import { HttpErrorResponse } from '@angular/common/http';
import { errorText } from '../../../shared/errorText';

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
    styleUrl: './register.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterComponent {
  private apiClient = inject(AccountService);
  private snackBar = inject(MatSnackBar);

  readonly registerForm = viewChild<NgForm>('registerForm');
  
  hidePassword = true;
  hideConfirmPassword = true;

  regex: string = String.raw`^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{12,}$`;
  regexErrorMessage: string = "Minimum 12 characters, uppercase letter, lowercase letter, number and special character #?!@$%^&*-";

  model: RegisterFormModel = {} as RegisterFormModel

  onSubmit() {
    const registerForm = this.registerForm();
    if (!registerForm) {
      return;
    }
    if (!registerForm.valid) {
      return;
    }

    this.apiClient.register(this.model).subscribe(
      {
        error: (e: HttpErrorResponse) => {
          console.log(e);
          this.snackBar.open(`Registration failed: ${errorText(e, 'the server refused it')}`, "Close");
        },
        next: (e) => this.snackBar.open("Registration Success", "Close"),
        //complete: () => console.log('Logged in'),
      });
  }
}
