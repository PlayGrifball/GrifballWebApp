import { formatNumber } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, LOCALE_ID, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import type { LeaderboardsDto, MetricLeaderDto } from '../api/dtos/leaderboardsDto';

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
  imports: [RouterLink],
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
    this.api.getLeaderboards().subscribe({
      next: boards => {
        this.boards.set(boards);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}
