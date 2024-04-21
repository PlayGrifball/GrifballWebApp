import { Component, OnInit } from '@angular/core';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { LoginDto } from '../api/dtos/loginDto';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { AccountService } from '../account.service';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';


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
    ErrorMessageComponent,
    MatCardModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent implements OnInit {
  hide = true;

  model: LoginDto = { } as LoginDto;

  constructor(private accountService: AccountService, private http: HttpClient, private route: ActivatedRoute) {
  }

  ngOnInit(): void {
    const callback = Boolean(this.route.snapshot.queryParamMap.get('callback'));
    if (callback === false)
      return;

    this.callback();
  }

  onSubmit() {
    //console.log(this.model.username);
    this.accountService.login(this.model);
  }

  callback() {
    this.accountService.loginExternal();
  }

  test() {
    this.http.get("/api/Identity/Test").subscribe(
      {
        error: (e) => console.log(e),
        next: (accessToken) => console.log('test next'),
        complete: () => console.log('test complete'),
      });
  }
}
