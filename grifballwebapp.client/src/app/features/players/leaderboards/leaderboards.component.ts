import { formatNumber } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, LOCALE_ID, OnInit, signal, viewChild } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import type { LeaderboardFilter, LeaderboardsDto, MetricLeaderDto, QuickSearchPlayerDto } from '@api/models';
import { leaderboardFormats } from '@shared/seasonFormats';
import type { Board } from './board';
import { ALL_FORMATS, LeaderboardsStore } from './leaderboards.store';
import { PlayerSearchComponent } from './playerSearch/playerSearch.component';
import { PlayerSpotlightComponent } from './playerSpotlight/playerSpotlight.component';

type MetricBoard = Exclude<Board, 'rings' | 'dynasties'>;

const METRICS: Record<MetricBoard, { title: string, list: (l: LeaderboardsDto) => MetricLeaderDto[] }> = {
  goals: { title: 'Goals', list: l => l.goalLeaders },
  kills: { title: 'Kills', list: l => l.killLeaders },
  games: { title: 'Games played', list: l => l.gamesPlayedLeaders },
  stops: { title: 'Carrier stops', list: l => l.carrierStopLeaders },
};

const isMetric = (board: Board): board is MetricBoard => board in METRICS;

/** The all-time leaderboards over every season: titles, dynasties and career totals, and a player's standing on each. */
@Component({
  selector: 'app-leaderboards',
  imports: [MatFormFieldModule, MatSelectModule, RouterLink, PlayerSearchComponent, PlayerSpotlightComponent],
  templateUrl: './leaderboards.component.html',
  styleUrl: './leaderboards.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [LeaderboardsStore],
})
export class LeaderboardsComponent implements OnInit {
  private readonly store = inject(LeaderboardsStore);
  private locale = inject(LOCALE_ID);

  readonly boards = this.store.boards;
  readonly loaded = this.store.loaded;
  readonly board = signal<Board>('rings');
  readonly formatOptions = leaderboardFormats;
  /** The formats every board counts, titles and stats alike: any of them, all of them to start with. */
  readonly formats = this.store.formats;
  /** The select's text: "All formats", or the formats chosen ("League season, Late League"). */
  readonly formatsLabel = computed(() => {
    const chosen = this.formats();
    return chosen.length === ALL_FORMATS.length ? 'All formats' : leaderboardFormats.filter(f => chosen.includes(f.value)).map(f => f.label).join(', ');
  });

  /** The text in the player search. */
  readonly searchQuery = signal('');
  readonly spotlight = this.store.spotlight;
  readonly spotlightLoading = this.store.spotlightLoading;
  readonly spotlightError = this.store.spotlightError;

  private readonly search = viewChild.required(PlayerSearchComponent);

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
    this.store.load();
  }

  /** Counts the formats chosen, in the select's order; none chosen counts them all again. */
  setFormats(chosen: LeaderboardFilter[]): void {
    this.store.setFormats(chosen);
  }

  selectPlayer(player: QuickSearchPlayerDto): void {
    this.store.lookUp({ query: player.name, userId: player.userID });
  }

  findPlayer(query: string): void {
    this.store.lookUp({ query, userId: null });
  }

  /** Closes the standing, and empties the search, dropping its pending suggestions too. */
  clearSpotlight(): void {
    this.search().reset();
    this.store.lookUp(null);
  }
}
