import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { CreateUserDto } from './createUserDto';
import { HttpClient } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
    selector: 'app-create-user',
    imports: [
        CommonModule,
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
    ],
    templateUrl: './createUser.component.html',
    styleUrl: './createUser.component.scss'
})
export class CreateUserComponent {
  model: CreateUserDto = {} as CreateUserDto;

  constructor(private http: HttpClient, private snackbar: MatSnackBar) {
  }


  onSubmit(): void {
    this.http.post("/api/usermanagement/createuser", this.model).subscribe({
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
