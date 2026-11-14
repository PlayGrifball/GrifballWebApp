import { HttpErrorResponse } from '@angular/common/http';
import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, input, numberAttribute, untracked } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountService } from '@core/auth/account.service';
import { FormsModule } from '@angular/forms';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DeploymentHistoryComponent } from './deploymentHistory/deploymentHistory.component';
import { LinkedAccountsComponent } from '@shared/linkedAccounts/linkedAccounts.component';
import { ProfileIdentityComponent } from './profileIdentity/profileIdentity.component';
import { AccessTokensComponent } from './accessTokens/accessTokens.component';
import { ServiceRecordComponent } from './serviceRecord/serviceRecord.component';
import { VodListComponent } from '@shared/vodList/vodList.component';
import { ProfileCommendationsComponent } from './profileCommendations/profileCommendations.component';
import { errorText } from '@shared/errorText';
import { whileCurrent } from '@shared/whileCurrent';
import type { GameCareerSummaryDto, MatchmakingPlayerStatDto, PlayerSeasonStatDetailDto, ProfileStatsDto } from '@api/models';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { ProfileService } from '@api/services/profile.service';
import { describePage, PageDescription, plural } from '@shared/pageMeta';

/** The player as search results and link previews show them: their gamertag, seasons and championships. */
export function describeProfile(profile: ProfileStatsDto): PageDescription | null {
  const name = profile.gamertag ?? profile.displayName;
  if (!name)
    return null;
  const facts = [
    // Seasons played (the career's count), not stat lines: a season has one per regular season, playoffs, stint and team
    profile.overallCareer?.seasonsCount ? plural(profile.overallCareer.seasonsCount, 'season') : null,
    profile.championships.length ? plural(profile.championships.length, 'championship') : null,
  ].filter(f => f);
  let career = `${name}'s Grifball career`;
  if (facts.length)
    career = `${career}: ${facts.join(', ')}`;
  return {
    title: name,
    description: `${career}. Stats by season and game, and Late League.`,
  };
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
    AccessTokensComponent,
    ServiceRecordComponent,
    VodListComponent,
    ProfileCommendationsComponent,
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileComponent {
  private profile = inject(ProfileService);
  accountService = inject(AccountService);
  private snackbar = inject(MatSnackBar);

  /** The :userID of the route. Angular keeps this page when only it changes (Profile in the header while on someone
   * else's profile), so a new one loads that user's profile in place of the one shown. */
  readonly userID = input.required({ transform: numberAttribute });
  readonly gamertag = signal<string | null>(null);
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

  /** Some of the Late League lines shown have games decided by vote: the table says what those count for. */
  readonly lateLeagueByVote = computed(() => this.filteredLateLeague().some(l => l.gamesDecidedByVote > 0));

  constructor() {
    describePage(() => {
      const profile = this.profileStats();
      return profile ? describeProfile(profile) : null;
    });
    effect(() => {
      this.userID();
      untracked(() => {
        // Nothing of the previous profile stays: its name, stats, chosen game or gamertag prompt
        this.gamertag.set(null);
        this.settingGamertag.set(false);
        this.profileStats.set(null);
        this.selectedGame.set('all');
        this.getGamertag();
        this.getProfileStats();
      });
    });
  }

  getGamertag(): void {
    const userID = this.userID();
    this.profile.getGamertag({ userID })
      .pipe(nullWhenNotFound(), whileCurrent(userID, this.userID))
      .subscribe({
        next: r => {
          this.gamertag.set(r?.gamertag ?? null);
          this.settingGamertag.set(this.accountService.personID() == userID && this.gamertag() === null);
        },
        error: e => {
          console.log(e);
          this.snackbar.open("Failed get gamertag", "Close");
        }
      });
  }

  getProfileStats(): void {
    const userID = this.userID();
    this.statsLoading.set(true);
    this.profile.getProfileStats({ userID })
      .pipe(whileCurrent(userID, this.userID))
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
    this.profile.setGamertag({ userID: this.userID(), body: { gamertag: this.gamertag() ?? '' } })
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
