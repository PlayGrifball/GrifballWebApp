import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { DecimalPipe } from '@angular/common';
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

export interface PlayerChampionshipDto {
  playerChampionshipID: number;
  seasonID: number;
  seasonName: string;
  seasonSourceUrl?: string | null;
  leagueID: number;
  leagueName: string;
  leagueSlug: string;
  gameVersionID?: number | null;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  tier: string;
  teamName: string;
  isCaptain: boolean;
  gamertagUsed: string;
  year?: number | null;
}

export interface GameCareerSummaryDto {
  gameVersionID?: number | null;
  gameVersionName: string;
  gameVersionSlug: string;
  seasonsCount: number;
  gamesPlayed: number;
  wins: number;
  losses: number;
  winPercent?: number | null;
  goals: number;
  goalsPerGame?: number | null;
  kills: number;
  deaths: number;
  assists: number;
  kdRatio?: number | null;
  kdSpread: number;
  carrierKills: number;
  stopsPerGame?: number | null;
  sprees: number;
  multiKills: number;
  exterminations: number;
  betrayals: number;
  hammerKills?: number | null;
  swordKills?: number | null;
  ballKills?: number | null;
  punches?: number | null;
  damageDealt?: number | null;
  damageTaken?: number | null;
  score?: number | null;
}

export interface PlayerSeasonStatDetailDto {
  playerSeasonStatID: number;
  seasonID: number;
  seasonName: string;
  seasonSourceUrl?: string | null;
  leagueID: number;
  leagueName: string;
  leagueSlug: string;
  gameVersionID: number;
  gameVersionName: string;
  gameVersionSlug: string;
  division?: string | null;
  teamName?: string | null;
  tradeNote?: string | null;
  roleNote?: string | null;
  stintIndex: number;
  isPlayoffs: boolean;
  isCombined: boolean;
  source: string;
  gamesPlayed: number;
  wins: number;
  losses: number;
  winPercent?: number | null;
  goals: number;
  kills: number;
  deaths: number;
  assists: number;
  kdRatio?: number | null;
  kdSpread?: number | null;
  carrierKills: number;
  sprees: number;
  multiKills: number;
  exterminations: number;
  damageDealt?: number | null;
  damageTaken?: number | null;
  score?: number | null;
  year?: number | null;
}

export interface ProfileStatsDto {
  userID: number;
  gamertag?: string | null;
  displayName?: string | null;
  isDummyUser: boolean;
  pastGamertags: string[];
  championships: PlayerChampionshipDto[];
  careerByGame: GameCareerSummaryDto[];
  overallCareer?: GameCareerSummaryDto | null;
  seasons: PlayerSeasonStatDetailDto[];
}

export interface ProfileDto {
  gamertag: string | null;
}

@Component({
  selector: 'app-profile',
  imports: [
    FormsModule,
    DecimalPipe,
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

  readonly profileStats = signal<ProfileStatsDto | null>(null);
  readonly statsLoading = signal(false);
  readonly selectedGame = signal<string>('all');

  readonly availableGames = computed(() => {
    const stats = this.profileStats();
    return stats?.careerByGame ?? [];
  });

  readonly activeCareer = computed<GameCareerSummaryDto | null>(() => {
    const stats = this.profileStats();
    if (!stats) return null;
    const current = this.selectedGame();
    if (current === 'all') return stats.overallCareer ?? null;
    return stats.careerByGame.find(g => g.gameVersionSlug === current) ?? null;
  });

  readonly filteredSeasons = computed<PlayerSeasonStatDetailDto[]>(() => {
    const stats = this.profileStats();
    if (!stats) return [];
    const current = this.selectedGame();
    if (current === 'all') return stats.seasons;
    return stats.seasons.filter(s => s.gameVersionSlug === current);
  });

  ngOnInit(): void {
    this.userID = Number(this.route.snapshot.paramMap.get('userID'));
    this.getGamertag();
    this.getProfileStats();
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

  getProfileStats(): void {
    this.statsLoading.set(true);
    this.http.get<ProfileStatsDto>("api/profile/getprofilestats/" + this.userID)
      .subscribe({
        next: s => {
          this.profileStats.set(s);
          if (s && !this.gamertag() && s.gamertag) {
            this.gamertag.set(s.gamertag);
          }
          this.statsLoading.set(false);
        },
        error: () => {
          this.statsLoading.set(false);
        }
      });
  }

  selectGame(slug: string): void {
    this.selectedGame.set(slug);
  }

  setGamertag(): void {
    const params = new HttpParams().set('gamertag', this.gamertag() ?? '');
    this.http.post("api/profile/setgamertag/" + this.userID, null, { params })
      .subscribe({
        next: () => {
          this.settingGamertag.set(false);
          // Setting a gamertag can merge placeholder accounts (and their historical stats) into this one
          this.getProfileStats();
        },
        error: (e: HttpErrorResponse) => {
          console.log(e);
          this.snackbar.open(errorText(e, "Failed set gamertag"), "Close");
        }
      });
  }
}
