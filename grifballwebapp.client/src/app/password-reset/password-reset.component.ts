import { Component, OnInit, input, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { UsePasswordResetLinkRequestDto } from '@api/models';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { AccountService } from '../account.service';
import { errorText } from '../shared/errorText';

@Component({
  selector: 'app-password-reset',
  imports: [
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    MatIconModule,
    RouterLink,
    ErrorMessageComponent
  ],
  templateUrl: './password-reset.component.html',
  styleUrl: './password-reset.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PasswordResetComponent implements OnInit {
  private router = inject(Router);
  private accountService = inject(AccountService);
  private snackBar = inject(MatSnackBar);

  token = input<string>();
  hide = true;
  hideConfirm = true;
  readonly isLoading = signal(false);
  
  model = {
    newPassword: '',
    confirmPassword: ''
  };

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

    this.isLoading.set(true);

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
        const errorMessage = errorText(error, error.status === 400 ? 'Invalid or expired reset link' : 'Failed to reset password');
        this.snackBar.open(errorMessage, 'Close');
        this.isLoading.set(false);
      }
    });
  }
}