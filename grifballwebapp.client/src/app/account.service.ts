import { Injectable, Signal, WritableSignal, computed, effect, signal } from '@angular/core';
import { ApiClientService } from './api/apiClient.service';
import { LoginDto } from './api/dtos/loginDto';
import { MatSnackBar } from '@angular/material/snack-bar';
import { JwtHelperService } from '@auth0/angular-jwt';
import { HttpClient } from '@angular/common/http';
import { AccessTokenResponse, MetaInfoResponse } from './accessTokenResponse';
import { RegisterDto } from './api/dtos/registerDto';
import { Observable, catchError, map, throwError, timer } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  accessTokenResponse: WritableSignal<AccessTokenResponse | undefined> = signal(undefined);

  accessToken: Signal<string | undefined> = computed(() => {
    const accessTokenResponse = this.accessTokenResponse();
    return accessTokenResponse?.accessToken;
  })

  isLoggedIn: Signal<boolean> = computed(() => {
    let token = this.accessToken();
    return token !== undefined;
  })

  isSysAdmin: WritableSignal<boolean> = signal(false);
  isEventOrganizer: WritableSignal<boolean> = signal(false);
  isPlayer: WritableSignal<boolean> = signal(false);
  personID: WritableSignal<number | null> = signal(null);
  displayName: WritableSignal<string | null> = signal(null);

  constructor(private apiClient: ApiClientService, private snackBar: MatSnackBar, private jwtHelper: JwtHelperService, private http: HttpClient) {
    let accessToken = localStorage.getItem("access_token");
    if (accessToken !== null) {
      const accessT: AccessTokenResponse = JSON.parse(accessToken)
      this.accessTokenResponse.set(accessT);
    }

    const metaInfoJson = localStorage.getItem("metaInfo");
    if (metaInfoJson !== null) {
      const r: MetaInfoResponse = JSON.parse(metaInfoJson);
      this.isSysAdmin.set(r.isSysAdmin);
      this.isEventOrganizer.set(r.isCommissioner);
      this.isPlayer.set(r.isPlayer);
      this.personID.set(r.userID);
      this.displayName.set(r.displayName);
    }

    // Danger zone, need to be careful to not cause
    effect(() => {
      const accessToken = this.accessToken();
      if (accessToken === undefined) {
        this.isSysAdmin.set(false);
        this.isEventOrganizer.set(false);
        this.isPlayer.set(false);
        this.personID.set(null);
        this.displayName.set(null);
      } else {
        this.http.get<MetaInfoResponse>('/api/identity/metaInfo').subscribe({
          next: r => {
            this.isSysAdmin.set(r.isSysAdmin);
            this.isEventOrganizer.set(r.isCommissioner);
            this.isPlayer.set(r.isPlayer);
            //this.personID.set(0);
            this.displayName.set(r.displayName);
            const json = JSON.stringify(r);
            localStorage.setItem("metaInfo", json);
          },
          error: e => {
            console.log('error getting meta info');
            this.isSysAdmin.set(false);
            this.isEventOrganizer.set(false);
            this.isPlayer.set(false);
            this.personID.set(null);
            this.displayName.set(null);
          }
        })
      }
    });
  }

  register(registerDto: RegisterDto) {
    return this.http.post('/api/identity/register', registerDto);
  }

  login(loginDto: LoginDto) : void {
    this.http.post<AccessTokenResponse>('/api/identity/login', loginDto).subscribe(
      {
        error: (e) => this.snackBar.open("Login failed", "Close"),
        next: (jwt) => this.onLoginSuccess(jwt),
      });
  }

  loginExternal(): void {
    this.http.get<AccessTokenResponse>("/api/Identity/ExternalLoginCallback").subscribe(
      {
        error: (e) => this.snackBar.open("Login failed", "Close"),
        next: (accessToken) => this.onLoginSuccess(accessToken),
      });
  }

  refresh(): null | Observable<AccessTokenResponse> {
    const rt = this.accessTokenResponse();
    if (rt === undefined)
      return null;

    const body = {
      refreshToken: rt.refreshToken
    }

    return this.http.post<AccessTokenResponse>("/api/Identity/Refresh", body)
      .pipe(catchError((err: any) => {
        console.log('Refresh failed, user must reauth');
        this.logout();
        return throwError(() => err);
      }))
      .pipe(map(r => {
        this.onLoginSuccess(r);
      return r;
      }))
  }

  private onLoginSuccess(accessToken: AccessTokenResponse) {
    this.accessTokenResponse.set(accessToken);

    timer((accessToken.expiresIn * 1000) / 2)
      .subscribe(() => {
        console.log('Time to refresh');
      });
    const json = JSON.stringify(accessToken);
    localStorage.setItem("access_token", json);
  }

  logout(): void {
    localStorage.removeItem("access_token");
    this.accessTokenResponse.set(undefined);
  }


}
