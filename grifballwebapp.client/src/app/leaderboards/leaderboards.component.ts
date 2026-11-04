import { DecimalPipe, formatNumber } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, LOCALE_ID, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import type { LeaderboardsDto, MetricLeaderDto } from '../api/dtos/leaderboardsDto';
import type { PlayerAllTimeRankDto, PlayerSearchResultDto } from '../api/dtos/playerAllTimeRankDto';
import { seasonFormats, type SeasonFormat } from '../api/dtos/seasonFormat';

export type Board = 'rings' | 'dynasties' | 'goals' | 'kills' | 'games' | 'stops';
type MetricBoard = Exclude<Board, 'rings' | 'dynasties'>;

const METRICS: Record<MetricBoard, { title: string, list: (l: LeaderboardsDto) => MetricLeaderDto[] }> = {
  goals: { title: 'Goals', list: l => l.goalLeaders },
  kills: { title: 'Kills', list: l => l.killLeaders },
  games: { title: 'Games played', list: l => l.gamesPlayedLeaders },
  stops: { title: 'Carrier stops', list: l => l.carrierStopLeaders },
};

const isMetric = (board: Board): board is MetricBoard => board in METRICS;

/** The all-time leaderboards over every season: titles, dynasties and career totals. */
@Component({
  selector: 'app-leaderboards',
  imports: [RouterLink, DecimalPipe],
  templateUrl: './leaderboards.component.html',
  styleUrl: './leaderboards.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaderboardsComponent implements OnInit {
  private api = inject(ApiClientService);
  private locale = inject(LOCALE_ID);

  readonly boards = signal<LeaderboardsDto | null>(null);
  readonly loaded = signal(false);
  readonly board = signal<Board>('rings');
  /** Whose titles the championship and dynasty boards count: one format's seasons, or all (null). */
  readonly format = signal<SeasonFormat | null>(null);
  readonly formats = seasonFormats;

  // Player Standing Search & Spotlight
  readonly searchQuery = signal('');
  readonly searchResults = signal<PlayerSearchResultDto[]>([]);
  readonly autocompleteOpen = signal(false);
  readonly spotlight = signal<PlayerAllTimeRankDto | null>(null);
  readonly spotlightLoading = signal(false);
  readonly spotlightError = signal<string | null>(null);
  private searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;

  readonly tabs: { id: Board, title: string }[] = [
    { id: 'rings', title: 'Championships' },
    { id: 'dynasties', title: 'Dynasties' },
    { id: 'goals', title: 'Goals' },
    { id: 'kills', title: 'Kills' },
    { id: 'games', title: 'Games played' },
    { id: 'stops', title: 'Carrier stops' },
  ];

  /** The table's accessible name: the chosen board's. */
  readonly tableLabel = computed(() => `${this.tabs.find(t => t.id === this.board())!.title} leaderboard`);

  /** The chosen career-total board, its per-game figures ready to show; null for the titles boards. */
  readonly metric = computed(() => {
    const board = this.board();
    const boards = this.boards();
    if (!boards || !isMetric(board))
      return null;
    return {
      title: METRICS[board].title,
      leaders: METRICS[board].list(boards).map(leader => ({
        ...leader,
        perGameText: leader.perGame == null ? '-' : formatNumber(leader.perGame, this.locale, '1.2-2'),
      })),
    };
  });

  ngOnInit(): void {
    this.load();
  }

  setFormat(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.format.set(value === '' ? null : value as SeasonFormat);
    this.load();
    const currentSpotlight = this.spotlight();
    if (currentSpotlight) {
      this.fetchPlayerRank(currentSpotlight.gamertag, currentSpotlight.userID);
    }
  }

  onSearchInput(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
    }

    const clean = value.trim();
    if (clean.length < 2) {
      this.searchResults.set([]);
      this.autocompleteOpen.set(false);
      return;
    }

    this.searchDebounceTimer = setTimeout(() => {
      this.api.searchPlayers(clean).subscribe({
        next: results => {
          this.searchResults.set(results);
          this.autocompleteOpen.set(results.length > 0);
        },
        error: () => this.searchResults.set([]),
      });
    }, 250);
  }

  selectPlayer(player: PlayerSearchResultDto): void {
    this.searchQuery.set(player.gamertag);
    this.autocompleteOpen.set(false);
    this.fetchPlayerRank(player.gamertag, player.userID);
  }

  submitSearch(): void {
    const q = this.searchQuery().trim();
    if (!q) return;
    this.autocompleteOpen.set(false);
    this.fetchPlayerRank(q);
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.searchResults.set([]);
    this.autocompleteOpen.set(false);
    this.spotlight.set(null);
    this.spotlightError.set(null);
  }

  closeAutocomplete(): void {
    // Delay closing so click on dropdown item triggers first
    setTimeout(() => this.autocompleteOpen.set(false), 200);
  }

  fetchPlayerRank(query: string, userId: number | null = null): void {
    this.spotlightLoading.set(true);
    this.spotlightError.set(null);

    this.api.getPlayerRank(query, userId, this.format()).subscribe({
      next: rank => {
        this.spotlight.set(rank);
        if (!rank) {
          this.spotlightError.set(`No player found matching "${query}".`);
        }
        this.spotlightLoading.set(false);
      },
      error: () => {
        this.spotlight.set(null);
        this.spotlightError.set(`Failed to load ranking for "${query}".`);
        this.spotlightLoading.set(false);
      },
    });
  }

  private load(): void {
    this.api.getLeaderboards(this.format()).subscribe({
      next: boards => {
        this.boards.set(boards);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}
