import { Component, OnInit, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { Router, RouterLink } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { UsePasswordResetLinkRequestDto } from '../api/dtos/passwordResetDtos';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { AccountService } from '../account.service';

@Component({
  selector: 'app-password-reset',
  imports: [
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    RouterLink,
    ErrorMessageComponent
  ],
  templateUrl: './password-reset.component.html',
  styleUrl: './password-reset.component.css'
})
export class PasswordResetComponent implements OnInit {
  token = input<string>();
  hide = true;
  hideConfirm = true;
  isLoading = false;
  
  model = {
    newPassword: '',
    confirmPassword: ''
  };

  constructor(
    private router: Router,
    private accountService: AccountService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    if (!this.token()) {
      this.snackBar.open('Invalid password reset link', 'Close');
      this.router.navigate(['/login']);
    }
  }

  onSubmit(): void {
    if (this.model.newPassword !== this.model.confirmPassword) {
      this.snackBar.open('Passwords do not match', 'Close');
      return;
    }

    if (this.model.newPassword.length < 6) {
      this.snackBar.open('Password must be at least 6 characters long', 'Close');
      return;
    }

    this.isLoading = true;

    const request: UsePasswordResetLinkRequestDto = {
      token: this.token()!,
      newPassword: this.model.newPassword
    };

    this.accountService.resetPassword(request).subscribe({
      next: (response) => {
        this.snackBar.open('Password reset successfully! You can now log in with your new password.', 'Close', {
          duration: 5000
        });
        this.router.navigate(['/login']);
      },
      error: (error) => {
        let errorMessage = 'Failed to reset password';
        if (error.error && typeof error.error === 'string') {
          errorMessage = error.error;
        } else if (error.status === 400) {
          errorMessage = 'Invalid or expired reset link';
        }
        this.snackBar.open(errorMessage, 'Close');
        this.isLoading = false;
      }
    });
  }
}