import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../../../../shared/validation/errorMessage.component';
import { CreateUserDto } from './createUserDto';
import { UserManagementService } from '@api/services/user-management.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
    selector: 'app-create-user',
    imports: [
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent
    ],
    templateUrl: './createUser.component.html',
    styleUrl: './createUser.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateUserComponent {
  private api = inject(UserManagementService);
  private snackbar = inject(MatSnackBar);

  model: CreateUserDto = {} as CreateUserDto;


  onSubmit(): void {
    this.api.createUser({ body: this.model }).subscribe({
      next: r => {
        this.snackbar.open('Created User');
      },
      error: e => {
        this.snackbar.open('Failed to create user');
        console.log(e);
      },
    })
  }
}
