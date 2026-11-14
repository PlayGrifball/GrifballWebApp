import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { UserManagementService } from '@api/services/user-management.service';
import { CreateUserDto } from './createUserDto';

/** Making a user. One per form (the component provides it). */
@Injectable()
export class CreateUserStore {
  private readonly api = inject(UserManagementService);
  private readonly snackbar = inject(MatSnackBar);

  /** Makes the user, and says how it went. */
  create(user: CreateUserDto): void {
    this.api.createUser({ body: user }).subscribe({
      next: () => this.snackbar.open('Created User'),
      error: e => {
        this.snackbar.open('Failed to create user');
        console.log(e);
      },
    });
  }
}
