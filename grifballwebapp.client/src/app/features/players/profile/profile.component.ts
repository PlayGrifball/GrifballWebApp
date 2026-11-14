import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, input, numberAttribute, untracked } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountService } from '@core/auth/account.service';
import { FormsModule } from '@angular/forms';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { DeploymentHistoryComponent } from './deploymentHistory/deploymentHistory.component';
import { LinkedAccountsComponent } from '@shared/linkedAccounts/linkedAccounts.component';
import { ProfileIdentityComponent } from './profileIdentity/profileIdentity.component';
import { AccessTokensComponent } from './accessTokens/accessTokens.component';
import { ServiceRecordComponent } from './serviceRecord/serviceRecord.component';
import { VodListComponent } from '@shared/vodList/vodList.component';
import { ProfileCommendationsComponent } from './profileCommendations/profileCommendations.component';
import type { GameCareerSummaryDto, MatchmakingPlayerStatDto, PlayerSeasonStatDetailDto, ProfileStatsDto } from '@api/models';
import { ProfileStore } from './profile.store';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ProfileStore],
})
export class ProfileComponent {
  /** The player's gamertag and stats, and the call that sets the gamertag. */
  readonly store = inject(ProfileStore);
  accountService = inject(AccountService);

  /** The :userID of the route. Angular keeps this page when only it changes (Profile in the header while on someone
   * else's profile), so a new one loads that user's profile in place of the one shown. */
  readonly userID = input.required({ transform: numberAttribute });
  /** The player's gamertag; on your own profile without one, what the prompt to set it holds. */
  readonly gamertag = this.store.gamertag;
  readonly settingGamertag = this.store.settingGamertag;

  readonly profileStats = this.store.profileStats;
  readonly statsLoading = this.store.statsLoading;
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
      const userID = this.userID();
      untracked(() => {
        // Nothing of the previous profile stays: its name, stats, chosen game or gamertag prompt
        this.selectedGame.set('all');
        this.store.show(userID);
      });
    });
  }

  selectGame(slug: string): void {
    this.selectedGame.set(slug);
  }

  setGamertag(): void {
    this.store.setGamertag();
  }
}
