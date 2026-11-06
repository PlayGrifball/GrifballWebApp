import { formatNumber } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, LOCALE_ID, OnInit, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, EMPTY, map, of, Subject, switchMap, tap } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import type { LeaderboardsDto, MetricLeaderDto } from '../api/dtos/leaderboardsDto';
import type { PlayerAllTimeRankDto, PlayerSearchResultDto } from '../api/dtos/playerAllTimeRankDto';
import { seasonFormats, type SeasonFormat } from '../api/dtos/seasonFormat';
import type { Board } from './board';
import { PlayerSearchComponent } from './player-search/player-search.component';
import { PlayerSpotlightComponent } from './player-spotlight/player-spotlight.component';

type MetricBoard = Exclude<Board, 'rings' | 'dynasties'>;

const METRICS: Record<MetricBoard, { title: string, list: (l: LeaderboardsDto) => MetricLeaderDto[] }> = {
  goals: { title: 'Goals', list: l => l.goalLeaders },
  kills: { title: 'Kills', list: l => l.killLeaders },
  games: { title: 'Games played', list: l => l.gamesPlayedLeaders },
  stops: { title: 'Carrier stops', list: l => l.carrierStopLeaders },
};

const isMetric = (board: Board): board is MetricBoard => board in METRICS;

/** A player to show the standing of: by user ID when picked from the suggestions, else by the text searched. */
interface Lookup { query: string, userId: number | null }

/** The all-time leaderboards over every season: titles, dynasties and career totals, and a player's standing on each. */
@Component({
  selector: 'app-leaderboards',
  imports: [RouterLink, PlayerSearchComponent, PlayerSpotlightComponent],
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

  /** The text in the player search. */
  readonly searchQuery = signal('');
  readonly spotlight = signal<PlayerAllTimeRankDto | null>(null);
  readonly spotlightLoading = signal(false);
  readonly spotlightError = signal<string | null>(null);

  private readonly search = viewChild.required(PlayerSearchComponent);

  /** The player last asked for (null: none), shown or still loading: a new format looks them up again. */
  private requested: Lookup | null = null;

  /** Each lookup (or null: none) cancels the one in flight, so an older player's standing never replaces a newer one. */
  private readonly lookups = new Subject<Lookup | null>();

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

  constructor() {
    this.lookups.pipe(
      tap(lookup => {
        this.spotlightError.set(null);
        this.spotlightLoading.set(lookup != null);
        if (!lookup) this.spotlight.set(null);
      }),
      switchMap(lookup => !lookup ? EMPTY : this.api.getPlayerRank(lookup.query, lookup.userId, this.format()).pipe(
        map(rank => ({ lookup, rank, failed: false })),
        catchError(() => of({ lookup, rank: null, failed: true })),
      )),
      takeUntilDestroyed(),
    ).subscribe(({ lookup, rank, failed }) => {
      this.spotlightLoading.set(false);
      this.spotlight.set(rank);
      if (!rank)
        this.spotlightError.set(failed ? `Failed to load ranking for "${lookup.query}".` : `No player found matching "${lookup.query}".`);
    });
  }

  ngOnInit(): void {
    this.load();
  }

  setFormat(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.format.set(value === '' ? null : value as SeasonFormat);
    this.load();
    // The standing's titles count the format too: the player last asked for, even while their standing is loading
    if (this.requested)
      this.lookUp(this.requested);
  }

  selectPlayer(player: PlayerSearchResultDto): void {
    this.lookUp({ query: player.gamertag, userId: player.userID });
  }

  findPlayer(query: string): void {
    this.lookUp({ query, userId: null });
  }

  /** Closes the standing, and empties the search, dropping its pending suggestions too. */
  clearSpotlight(): void {
    this.search().reset();
    this.lookUp(null);
  }

  private lookUp(lookup: Lookup | null): void {
    this.requested = lookup;
    this.lookups.next(lookup);
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
