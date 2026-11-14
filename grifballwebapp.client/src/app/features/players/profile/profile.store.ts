import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { ProfileStatsDto } from '@api/models';
import { ProfileService } from '@api/services/profile.service';
import { AccountService } from '@core/auth/account.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { errorText } from '@shared/errorText';
import { whileCurrent } from '@shared/whileCurrent';

/**
 * A player's profile: their gamertag (and, on your own profile without one, the prompt to set it) and their stats, and
 * the call that sets the gamertag. One per profile page (the component provides it).
 */
@Injectable()
export class ProfileStore {
  private readonly profile = inject(ProfileService);
  private readonly accountService = inject(AccountService);
  private readonly snackbar = inject(MatSnackBar);

  /** The player shown, set by {@link show}. */
  private readonly userID = signal(0);
  /** The player's gamertag; on your own profile without one, what the prompt to set it holds. */
  readonly gamertag = signal<string | null>(null);
  /** Your own profile has no gamertag: the page asks you for it. */
  private readonly _settingGamertag = signal(false);
  readonly settingGamertag = this._settingGamertag.asReadonly();

  private readonly _profileStats = signal<ProfileStatsDto | null>(null);
  readonly profileStats = this._profileStats.asReadonly();
  private readonly _statsLoading = signal(false);
  readonly statsLoading = this._statsLoading.asReadonly();

  /** Shows a player's profile. Nothing of the previous one stays: its name, stats or gamertag prompt. */
  show(userID: number): void {
    this.userID.set(userID);
    this.gamertag.set(null);
    this._settingGamertag.set(false);
    this._profileStats.set(null);
    this.loadGamertag();
    this.loadStats();
  }

  private loadGamertag(): void {
    const userID = this.userID();
    this.profile.getGamertag({ userID })
      .pipe(nullWhenNotFound(), whileCurrent(userID, this.userID))
      .subscribe({
        next: r => {
          this.gamertag.set(r?.gamertag ?? null);
          this._settingGamertag.set(this.accountService.personID() == userID && this.gamertag() === null);
        },
        error: e => {
          console.log(e);
          this.snackbar.open("Failed get gamertag", "Close");
        }
      });
  }

  /** Loads the player's stats (again). */
  loadStats(): void {
    const userID = this.userID();
    this._statsLoading.set(true);
    this.profile.getProfileStats({ userID })
      .pipe(whileCurrent(userID, this.userID))
      .subscribe({
        next: s => {
          this._profileStats.set(s);
          if (s && !this.gamertag() && s.gamertag) {
            this.gamertag.set(s.gamertag);
          }
          this._statsLoading.set(false);
        },
        error: () => {
          this._statsLoading.set(false);
        }
      });
  }

  /** Sets your gamertag to what the prompt holds. */
  setGamertag(): void {
    this.profile.setGamertag({ userID: this.userID(), body: { gamertag: this.gamertag() ?? '' } })
      .subscribe({
        next: () => {
          this._settingGamertag.set(false);
          // Setting a gamertag can merge placeholder accounts (and their historical stats) into this one
          this.loadStats();
        },
        error: (e: HttpErrorResponse) => {
          console.log(e);
          this.snackbar.open(errorText(e, "Failed set gamertag"), "Close");
        }
      });
  }
}
