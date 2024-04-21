import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { UserResponseDto } from '../userResponseDto';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatCheckboxModule } from '@angular/material/checkbox';

@Component({
  selector: 'app-edit-user',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    MtxDatetimepickerModule,
    MatCheckboxModule
  ],
  templateUrl: './editUser.component.html',
  styleUrl: './editUser.component.scss'
})
export class EditUserComponent {
  model: UserResponseDto = {} as UserResponseDto;

  constructor(private route: ActivatedRoute, private http: HttpClient) { }

  ngOnInit(): void {
    const userID = Number(this.route.snapshot.paramMap.get('userID'));

    if (userID === 0) {
      console.log('Invalid user ID');
      return;
    }

    // Fetch from backend
    this.http.get<UserResponseDto>('/api/usermanagement/getuser/' + userID)
      .subscribe({
        next: (result) => this.model = result,
        error: (error) => console.log(error)
      });
  }

  onSubmit() {
    this.http.post('/api/usermanagement/edituser/', this.model).subscribe({
      next: (result) => console.log('Updated user'),
      error: (result) => console.log(result),
    });
  }

}
