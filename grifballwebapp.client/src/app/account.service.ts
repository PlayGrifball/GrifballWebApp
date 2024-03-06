import { Injectable, Signal, WritableSignal, computed, signal } from '@angular/core';
import { ApiClientService } from './api/apiClient.service';
import { LoginDto } from './api/dtos/loginDto';
import { MatSnackBar } from '@angular/material/snack-bar';
import { JwtHelperService } from '@auth0/angular-jwt';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  exp: WritableSignal<Date | null> = signal(null);

  isLoggedIn: Signal<boolean> = computed(() => {

    let a = new Date(Date.now());
    let b = this.exp();
    let loggedOut = b === null || a >= b;
    return !loggedOut;
  })
  constructor(private apiClient: ApiClientService, private snackBar: MatSnackBar, private jwtHelper: JwtHelperService) {
    let jwt = localStorage.getItem("access_token");
    if (jwt !== null) {
      this.exp.set(this.jwtHelper.getTokenExpirationDate(jwt));
    }
  }

  login(loginDto: LoginDto) : void {
    this.apiClient.login(loginDto).subscribe(
      {
        error: (e) => this.snackBar.open("Login failed", "Close"),
        next: (jwt) => this.onLoginSuccess(jwt),
        complete: () => console.log('Logged in'),
      });
  }

  private onLoginSuccess(jwt: string) {
    console.log("before: " + this.isLoggedIn());
    this.exp.set(this.jwtHelper.getTokenExpirationDate(jwt));
    localStorage.setItem("access_token", jwt);
    console.log("after: " + this.isLoggedIn());
  }
}
