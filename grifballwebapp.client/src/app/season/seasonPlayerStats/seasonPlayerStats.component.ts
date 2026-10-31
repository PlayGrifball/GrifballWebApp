import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

/** A player's totals for one stint of the season (regular season or playoffs). */
export interface SeasonPlayerStatDto {
  /** The stored row, for stored season totals; null for stats from the season's games. */
  playerSeasonStatID: number | null;
  userID: number;
  gamertag: string;
  teamID?: number | null;
  teamName?: string | null;
  division?: string | null;
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
}

export type StatPhase = 'all' | 'regular' | 'playoffs';

/** The season's player stats: a sortable leaderboard, by phase, filtered by gamertag or team. */
@Component({
  selector: 'app-season-player-stats',
  imports: [RouterLink],
  templateUrl: './seasonPlayerStats.component.html',
  styleUrl: './seasonPlayerStats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonPlayerStatsComponent {
  private http = inject(HttpClient);

  readonly seasonID = input.required<number>();
  readonly stats = signal<SeasonPlayerStatDto[]>([]);
  readonly loaded = signal(false);
  readonly phase = signal<StatPhase>('all');
  readonly query = signal('');

  readonly phases: { id: StatPhase; title: string }[] = [
    { id: 'all', title: 'All' },
    { id: 'regular', title: 'Regular season' },
    { id: 'playoffs', title: 'Playoffs' },
  ];

  readonly shown = computed(() => {
    const phase = this.phase();
    const query = this.query().trim().toLowerCase();
    return this.stats().filter(p =>
      (phase === 'all' || p.isPlayoffs === (phase === 'playoffs')) &&
      (!query || p.gamertag.toLowerCase().includes(query) || (p.teamName?.toLowerCase().includes(query) ?? false)));
  });

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      this.loaded.set(false);
      this.http.get<SeasonPlayerStatDto[]>('/api/Season/GetSeasonPlayerStats/' + seasonID).subscribe({
        next: stats => {
          this.stats.set(stats);
          this.loaded.set(true);
        },
        error: () => this.loaded.set(true),
      });
    });
  }

  search(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }
}
