import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AccountService } from '../account.service';
import { FormsModule } from '@angular/forms';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ErrorMessageComponent,
    MatInputModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSnackBarModule
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent implements OnInit {
  gamertag: string | null = null;
  userID: number = 0;
  settingGamertag: boolean = false;

  constructor(private route: ActivatedRoute, private http: HttpClient, public accountService: AccountService, private snackbar: MatSnackBar) {}

  ngOnInit(): void {
    this.userID = Number(this.route.snapshot.paramMap.get('userID'));

    this.getGamertag();

    
  }

  getGamertag(): void {
    this.http.get<ProfileDto | null>("api/profile/getgamertag/" + this.userID)
      .subscribe({
        next: r => {
          this.gamertag = r?.gamertag ?? null;
          this.settingGamertag = this.accountService.personID() == this.userID && this.gamertag === null;
        },
        error: e => {
          console.log(e);
          this.snackbar.open("Failed get gamertag", "Close");
        }
      });
  }

  setGamertag(): void {
    this.http.get("api/profile/setgamertag/" + this.userID + "?gamertag=" + this.gamertag)
      .subscribe({
        next: r => {
          this.gamertag = this.gamertag;
          this.settingGamertag = false;
        },
        error: e => {
          console.log(e);
          this.snackbar.open("Failed set gamertag", "Close");
        }
      });
  }
}

export interface ProfileDto {
  gamertag: string | null
}
