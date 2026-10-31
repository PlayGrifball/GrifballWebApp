import { HttpClient } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

export interface HistoricalCatalogChampionDto {
  tier: string;
  teamID: number;
  teamName: string;
  roster: { userID: number; gamertag: string; isCaptain: boolean }[];
}

export interface HistoricalCatalogItemDto {
  seasonID: number;
  seasonName: string;
  year?: number | null;
  leagueID: number;
  leagueName: string;
  leagueSlug: string;
  gameVersionID?: number | null;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  division?: string | null;
  isHistorical: boolean;
  historicalSourceUrl?: string | null;
  teamCount: number;
  playerCount: number;
  champions: HistoricalCatalogChampionDto[];
}

export interface HistoricalPlayerRingLeaderDto {
  userID: number;
  gamertag: string;
  ringsCount: number;
  titles: string[];
}

/** A team name's titles across seasons, linked to its latest title-winning team. */
export interface HistoricalTeamRingLeaderDto {
  teamName: string;
  latestTeamID: number;
  latestSeasonID: number;
  championshipsCount: number;
  seasonsActive: number;
  titles: string[];
}

export interface HistoricalPlayerMetricLeaderDto {
  userID: number;
  gamertag: string;
  statValue: number;
  gamesPlayed: number;
  perGame?: number | null;
  seasonsCount: number;
  secondaryStat?: string | null;
}

export interface HistoricalLeaderboardsDto {
  ringLeaders: HistoricalPlayerRingLeaderDto[];
  dynasties: HistoricalTeamRingLeaderDto[];
  goalLeaders: HistoricalPlayerMetricLeaderDto[];
  killLeaders: HistoricalPlayerMetricLeaderDto[];
  gamesPlayedLeaders: HistoricalPlayerMetricLeaderDto[];
  carrierStopLeaders: HistoricalPlayerMetricLeaderDto[];
}

@Component({
  selector: 'app-archive-hub',
  imports: [RouterLink, FormsModule],
  templateUrl: './archive-hub.component.html',
  styleUrl: './archive-hub.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ArchiveHubComponent implements OnInit {
  private http = inject(HttpClient);

  readonly catalog = signal<HistoricalCatalogItemDto[]>([]);
  readonly leaderboards = signal<HistoricalLeaderboardsDto | null>(null);

  readonly loadingCatalog = signal<boolean>(true);
  readonly loadingLeaderboards = signal<boolean>(false);

  readonly activeTab = signal<'catalog' | 'leaderboards'>('catalog');
  readonly selectedGame = signal<string>('all');
  readonly selectedLeague = signal<string>('all');
  readonly searchQuery = signal<string>('');
  readonly leaderboardMetric = signal<'rings' | 'dynasties' | 'goals' | 'kills' | 'games' | 'stops'>('rings');

  readonly availableGames = computed(() => {
    const games = new Set<string>();
    for (const item of this.catalog()) {
      if (item.gameVersionName) games.add(item.gameVersionName);
    }
    return Array.from(games).sort();
  });

  readonly availableLeagues = computed(() => {
    const leagues = new Set<string>();
    for (const item of this.catalog()) {
      if (item.leagueName) leagues.add(item.leagueName);
    }
    return Array.from(leagues).sort();
  });

  readonly filteredCatalog = computed(() => {
    let list = this.catalog();

    const game = this.selectedGame();
    if (game !== 'all') {
      list = list.filter(item => item.gameVersionName === game);
    }

    const league = this.selectedLeague();
    if (league !== 'all') {
      list = list.filter(item => item.leagueName === league);
    }

    const query = this.searchQuery().trim().toLowerCase();
    if (query) {
      list = list.filter(item =>
        item.seasonName.toLowerCase().includes(query) ||
        (item.year && item.year.toString().includes(query)) ||
        item.champions.some(c => c.teamName.toLowerCase().includes(query))
      );
    }

    return list;
  });

  readonly totalChampionshipsCount = computed(() => {
    return this.catalog().reduce((acc, item) => acc + item.champions.length, 0);
  });

  ngOnInit(): void {
    this.http.get<HistoricalCatalogItemDto[]>('/api/Season/GetSeasonCatalog')
      .subscribe({
        next: (data) => {
          this.catalog.set(data);
          this.loadingCatalog.set(false);
        },
        error: () => {
          this.loadingCatalog.set(false);
        }
      });
  }

  setTab(tab: 'catalog' | 'leaderboards'): void {
    this.activeTab.set(tab);
    if (tab === 'leaderboards' && !this.leaderboards() && !this.loadingLeaderboards()) {
      this.loadLeaderboards();
    }
  }

  setMetric(metric: 'rings' | 'dynasties' | 'goals' | 'kills' | 'games' | 'stops'): void {
    this.leaderboardMetric.set(metric);
  }

  setGame(game: string): void {
    this.selectedGame.set(game);
  }

  setLeague(league: string): void {
    this.selectedLeague.set(league);
  }

  private loadLeaderboards(): void {
    this.loadingLeaderboards.set(true);
    this.http.get<HistoricalLeaderboardsDto>('/api/Leaderboards/GetLeaderboards')
      .subscribe({
        next: (data) => {
          this.leaderboards.set(data);
          this.loadingLeaderboards.set(false);
        },
        error: () => {
          this.loadingLeaderboards.set(false);
        }
      });
  }
}
