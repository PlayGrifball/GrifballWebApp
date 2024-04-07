import { Injectable, Signal, WritableSignal, computed, signal } from '@angular/core';
import { ApiClientService } from './api/apiClient.service';
import { LoginDto } from './api/dtos/loginDto';
import { MatSnackBar } from '@angular/material/snack-bar';
import { JwtHelperService } from '@auth0/angular-jwt';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  jwt: WritableSignal<string | null> = signal(null);

  exp: Signal<Date | null> = computed(() => {

    let x = this.jwt();

    if (x === null) {
      return null;
    }

    return this.jwtHelper.getTokenExpirationDate(x);
  })

  isLoggedIn: Signal<boolean> = computed(() => {

    let a = new Date(Date.now());
    let b = this.exp();
    let loggedOut = b === null || a >= b;
    return !loggedOut;
  })

  isEventOrganizer: Signal<boolean> = computed(() => {
    const y = this.isLoggedIn();
    const x = this.jwt();

    if (y === false) {
      return false;
    }

    if (x === null) {
      return false;
    }

    const token = this.jwtHelper.decodeToken(x);
    if (!token.hasOwnProperty("role")) {
      return false;
    }

    const role = token.role as string | string[];

    if (Array.isArray(role)) {
      return role.includes("EventOrganizer");
    }
    else {
      return role === "EventOrganizer";
    }
  })

  personID: Signal<number | null> = computed(() => {
    const y = this.isLoggedIn();
    const x = this.jwt();

    if (y === false) {
      return null;
    }

    if (x === null) {
      return null;
    }

    const token = this.jwtHelper.decodeToken(x);
    if (!token.hasOwnProperty("PersonID")) {
      return null;
    }

    const personID = token.PersonID as string;
    return +personID;
  })

  constructor(private apiClient: ApiClientService, private snackBar: MatSnackBar, private jwtHelper: JwtHelperService) {
    let jwt = localStorage.getItem("access_token");
    if (jwt !== null) {
      //this.exp.set(this.jwtHelper.getTokenExpirationDate(jwt));
      this.jwt.set(jwt);
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
    //this.exp.set(this.jwtHelper.getTokenExpirationDate(jwt));
    this.jwt.set(jwt);
    localStorage.setItem("access_token", jwt);
  }

  logout(): void {
    localStorage.removeItem("access_token");
    this.jwt.set(null);
  }
}
