import { Component, input, OnInit } from '@angular/core';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { LoginDto } from '../api/dtos/loginDto';
import { Router, RouterLink } from '@angular/router';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { AccountService } from '../account.service';
import { MatCardModule } from '@angular/material/card';

@Component({
    selector: 'app-login',
    imports: [
        MatInputModule,
        MatFormFieldModule,
        FormsModule,
        MatIconModule,
        MatButtonModule,
        RouterLink,
        ErrorMessageComponent,
        MatCardModule
    ],
    templateUrl: './login.component.html',
    styleUrl: './login.component.css'
})
export class LoginComponent implements OnInit {
  hide = true;

  model: LoginDto = { } as LoginDto;

  callback = input<string>();
  followUp = input<string>();

  constructor(public accountService: AccountService, private router: Router) {
  }

  ngOnInit(): void {
    if (this.callback() === 'true') {
      // We are returning from a login, validate that it worked
      console.log('Returned from backend ExternalLoginCallback');
      this.accountService.loginExternal(this.followUp() ?? '');
    } else { // Just loading the page... still check for a follow-up
      if (this.followUp() !== undefined && this.followUp() !== '') {
        if (this.accountService.isLoggedIn()) {
          // Go right to the follow-up
          console.log('Already logged in, redirecting to follow-up');
          this.router.navigate([this.followUp()!]);
        } else {
          console.log('Not logged in, must log in before going to follow-up');
          window.location.href = `/api/identity/externallogin?followUp=${this.followUp()}`;
        }
      }
    }
  }

  onSubmit() {
    this.accountService.login(this.model);
  }
}
