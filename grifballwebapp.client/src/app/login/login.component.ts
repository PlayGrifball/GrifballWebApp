import { Component } from '@angular/core';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { LoginDto } from '../api/dtos/loginDto';
import { RouterLink } from '@angular/router';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { AccountService } from '../account.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    MatInputModule,
    MatFormFieldModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    RouterLink,
    ErrorMessageComponent
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  hide = true;

  model: LoginDto = { } as LoginDto;

  constructor(private accountService: AccountService) {
  }

  onSubmit() {
    //console.log(this.model.username);
    this.accountService.login(this.model);
  }
}
