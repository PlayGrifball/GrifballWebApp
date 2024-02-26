import { Component } from '@angular/core';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { LoginDto } from './loginDto';
import { ApiClientService } from '../../ApiClient.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    MatInputModule,
    MatFormFieldModule,
    FormsModule,
    MatIconModule,
    MatButtonModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  hide = true;

  model: LoginDto = { } as LoginDto;

  constructor(private apiClient: ApiClientService) {
    console.log(this.model.username);
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
