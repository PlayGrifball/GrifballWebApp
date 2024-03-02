import { Component, ViewChild } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ApiClientService } from '../../ApiClient.service';
import { RouterLink } from '@angular/router';
import { RegisterFormModel } from './registerFormModel';
import { ErrorMessageComponent } from '../errorMessage/errorMessage.component';
import { PasswordMatchValidatorDirectiveDirective } from '../regexMatchValidValidatorDirective.directive';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    MatInputModule,
    MatFormFieldModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    RouterLink,
    ErrorMessageComponent,
    PasswordMatchValidatorDirectiveDirective
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  @ViewChild('registerForm') snav!: NgForm;
  
  hidePassword = true;
  hideConfirmPassword = true;

  //regex: string = "bob";
  regex: string = "(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}";
  //regex: string = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[`~!@#$%^&*()\-_=+[{\]}\]\\\|;:'\",<\.>\/?])[A-Za-z\d`~!@#$%^&*()\-_=+[{\]}\]\\\|;:'\",<\.>\/?]{6,}$";
  regexErrorMessage: string = "Minimum eight characters, at least one uppercase letter, one lowercase letter, one number and one special character";

  model: RegisterFormModel = {} as RegisterFormModel;

  constructor(private apiClient: ApiClientService) {
    //let u = this.snav.errors || {}
    //console.log(u);
  }

  onSubmit() {
    //console.log(this.model.username);
    this.apiClient.login(this.model).subscribe(
      {
        error: (e) => console.log(e),
        next: (e) => console.log('Next: ' + e),
        complete: () => console.log('Logged in'),
      });
  }
}
