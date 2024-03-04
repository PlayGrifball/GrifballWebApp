import { Component } from '@angular/core';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { LoginDto } from './loginDto';
import { ApiClientService } from '../../ApiClient.service';
import { RouterLink } from '@angular/router';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { MatSnackBar } from '@angular/material/snack-bar';

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

  constructor(private apiClient: ApiClientService, private snackBar: MatSnackBar) {
  }

  onSubmit() {
    //console.log(this.model.username);
    this.apiClient.login(this.model).subscribe(
      {
        error: (e) => this.snackBar.open("Login failed", "Close"),
        next: (e) => this.snackBar.open("Login Success", "Close"),
        complete: () => console.log('Logged in'),
      });
  }
}
