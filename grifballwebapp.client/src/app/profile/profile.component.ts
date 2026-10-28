import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AccountService } from '../account.service';
import { FormsModule } from '@angular/forms';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { LinkedAccountsComponent } from '../shared/linkedAccounts/linkedAccounts.component';
import { errorText } from '../shared/errorText';

@Component({
    selector: 'app-profile',
    imports: [
        FormsModule,
        ErrorMessageComponent,
        MatInputModule,
        MatButtonModule,
        MatFormFieldModule,
        MatSnackBarModule,
        LinkedAccountsComponent
    ],
    templateUrl: './profile.component.html',
    styleUrl: './profile.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  accountService = inject(AccountService);
  private snackbar = inject(MatSnackBar);

  readonly gamertag = signal<string | null>(null);
  userID: number = 0;
  readonly settingGamertag = signal(false);

  ngOnInit(): void {
    this.userID = Number(this.route.snapshot.paramMap.get('userID'));

    this.getGamertag();

    
  }

  getGamertag(): void {
    this.http.get<ProfileDto | null>("api/profile/getgamertag/" + this.userID)
      .subscribe({
        next: r => {
          this.gamertag.set(r?.gamertag ?? null);
          this.settingGamertag.set(this.accountService.personID() == this.userID && this.gamertag() === null);
        },
        error: e => {
          console.log(e);
          this.snackbar.open("Failed get gamertag", "Close");
        }
      });
  }

  setGamertag(): void {
    const params = new HttpParams().set('gamertag', this.gamertag() ?? '');
    this.http.get("api/profile/setgamertag/" + this.userID, { params })
      .subscribe({
        next: r => {
          this.settingGamertag.set(false);
        },
        error: (e: HttpErrorResponse) => {
          console.log(e);
          this.snackbar.open(errorText(e, "Failed set gamertag"), "Close");
        }
      });
  }
}

export interface ProfileDto {
  gamertag: string | null
}
