import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';

export interface RingLeaderDto { userID: number; gamertag: string; ringsCount: number; titles: string[]; }

/** A team name's titles across seasons, linked to its latest title-winning team. */
export interface DynastyDto {
  teamName: string;
  latestTeamID: number;
  latestSeasonID: number;
  championshipsCount: number;
  seasonsActive: number;
  titles: string[];
}

export interface MetricLeaderDto {
  userID: number;
  gamertag: string;
  statValue: number;
  gamesPlayed: number;
  perGame?: number | null;
  seasonsCount: number;
  secondaryStat?: string | null;
}

export interface LeaderboardsDto {
  ringLeaders: RingLeaderDto[];
  dynasties: DynastyDto[];
  goalLeaders: MetricLeaderDto[];
  killLeaders: MetricLeaderDto[];
  gamesPlayedLeaders: MetricLeaderDto[];
  carrierStopLeaders: MetricLeaderDto[];
}

export type Board = 'rings' | 'dynasties' | 'goals' | 'kills' | 'games' | 'stops';

const METRICS: Record<'goals' | 'kills' | 'games' | 'stops', { title: string, list: (l: LeaderboardsDto) => MetricLeaderDto[] }> = {
  goals: { title: 'Goals', list: l => l.goalLeaders },
  kills: { title: 'Kills', list: l => l.killLeaders },
  games: { title: 'Games played', list: l => l.gamesPlayedLeaders },
  stops: { title: 'Carrier stops', list: l => l.carrierStopLeaders },
};

/** The all-time leaderboards over every season: titles, dynasties and career totals. */
@Component({
  selector: 'app-leaderboards',
  imports: [RouterLink, DecimalPipe],
  templateUrl: './leaderboards.component.html',
  styleUrl: './leaderboards.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaderboardsComponent implements OnInit {
  private http = inject(HttpClient);

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

  /** The chosen career-total board, null for the titles boards. */
  readonly metric = computed(() => {
    const board = this.board();
    const boards = this.boards();
    if (!boards || board === 'rings' || board === 'dynasties')
      return null;
    return { title: METRICS[board].title, leaders: METRICS[board].list(boards) };
  });

  ngOnInit(): void {
    this.http.get<LeaderboardsDto>('/api/Leaderboards/GetLeaderboards').subscribe({
      next: boards => {
        this.boards.set(boards);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}
