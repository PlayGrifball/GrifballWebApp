import type { MatchmakingPlayerStatDto } from '../api/dtos/lateLeagueDto';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountService } from '../account.service';
import { FormsModule } from '@angular/forms';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DeploymentHistoryComponent } from './deploymentHistory/deploymentHistory.component';
import { LinkedAccountsComponent } from '../shared/linkedAccounts/linkedAccounts.component';
import { ProfileIdentityComponent } from './profileIdentity/profileIdentity.component';
import { VodListComponent } from '../shared/vodList/vodList.component';
import { ProfileCommendationsComponent } from './profileCommendations/profileCommendations.component';
import { errorText } from '../shared/errorText';
import type { SeasonInfoDto } from '../api/dtos/seasonInfoDto';
import type { SeasonPlayerStatDto } from '../api/dtos/seasonPlayerStatDto';

export interface PlayerChampionshipDto {
  playerChampionshipID: number;
  season: SeasonInfoDto;
  /** The game the title was won on, which can differ from the season's. */
  gameVersionID?: number | null;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  tier: string;
  teamID: number;
  teamName: string;
  isCaptain: boolean;
  gamertagUsed: string;
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
  /** Null on the all-games row: goals, carrier stops and medals are only totalled per game. */
  goals: number | null;
  goalsPerGame?: number | null;
  kills: number;
  deaths: number;
  assists: number;
  kdRatio?: number | null;
  kdSpread: number;
  carrierKills: number | null;
  stopsPerGame?: number | null;
  sprees: number | null;
  multiKills: number | null;
  exterminations: number | null;
  betrayals: number | null;
  hammerKills?: number | null;
  swordKills?: number | null;
  ballKills?: number | null;
  punches?: number | null;
  damageDealt?: number | null;
  damageTaken?: number | null;
  score?: number | null;
}

export interface PlayerSeasonStatDetailDto extends SeasonPlayerStatDto {
  season: SeasonInfoDto;
  /** The line's game, which can differ from the season's. */
  gameVersionID: number;
  gameVersionName: string;
  gameVersionSlug: string;
  damageDealt?: number | null;
  damageTaken?: number | null;
  score?: number | null;
}

export interface ProfileStatsDto {
  userID: number;
  gamertag?: string | null;
  displayName?: string | null;
  isDummyUser: boolean;
  /** Where the player plays from (Other.Regions); null when not set. */
  regionID?: number | null;
  regionName?: string | null;
  pastGamertags: string[];
  championships: PlayerChampionshipDto[];
  careerByGame: GameCareerSummaryDto[];
  overallCareer?: GameCareerSummaryDto | null;
  seasons: PlayerSeasonStatDetailDto[];
  /** Late League (matchmaking): a line per matchmaking season, and one for the queue's games outside any. */
  lateLeague: MatchmakingPlayerStatDto[];
}

export interface ProfileDto {
  gamertag: string | null;
}

@Component({
  selector: 'app-profile',
  imports: [
    RouterLink,
    FormsModule,
    DecimalPipe,
    ErrorMessageComponent,
    MatInputModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSnackBarModule,
    DeploymentHistoryComponent,
    LinkedAccountsComponent,
    ProfileIdentityComponent,
    VodListComponent,
    ProfileCommendationsComponent,
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

  /** The commendations panel holds Halo Infinite medals, so it shows for all games or Infinite only. */
  readonly showCommendations = computed(() => {
    const game = this.selectedGame();
    return game === 'all' || game === 'infinite';
  });

  /** The player's Late League lines, of the game chosen. */
  readonly filteredLateLeague = computed<MatchmakingPlayerStatDto[]>(() => {
    const lines = this.profileStats()?.lateLeague ?? [];
    const current = this.selectedGame();
    return current === 'all' ? lines : lines.filter(l => l.gameVersionSlug === current);
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
