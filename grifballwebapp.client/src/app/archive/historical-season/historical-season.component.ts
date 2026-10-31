import { HttpClient } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

export interface HistoricalSeasonChampionDto {
  tier: string;
  teamName: string;
  teamSlug: string;
  roster: { userID: number; gamertag: string; isCaptain: boolean }[];
}

export interface HistoricalTeamPlayerDto {
  userID: number;
  gamertag: string;
  tradeNote?: string | null;
  roleNote?: string | null;
  stintIndex: number;
  gamesPlayed: number;
  goals: number;
  kills: number;
  deaths: number;
  kdRatio?: number | null;
  carrierKills: number;
}

export interface HistoricalSeasonTeamDto {
  teamName: string;
  teamSlug: string;
  gamesPlayed: number;
  wins: number;
  losses: number;
  winPercent?: number | null;
  goals: number;
  wonChampionship: boolean;
  roster: HistoricalTeamPlayerDto[];
}

export interface HistoricalSeasonPlayerStatDto {
  playerSeasonStatID: number;
  userID: number;
  gamertag: string;
  teamName?: string | null;
  teamSlug?: string | null;
  division?: string | null;
  tradeNote?: string | null;
  roleNote?: string | null;
  stintIndex: number;
  isPlayoffs: boolean;
  isCombined: boolean;
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

export interface HistoricalSeasonDto {
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
  historicalSourceUrl?: string | null;
  champions: HistoricalSeasonChampionDto[];
  teams: HistoricalSeasonTeamDto[];
  playerStats: HistoricalSeasonPlayerStatDto[];
}

@Component({
  selector: 'app-historical-season',
  imports: [RouterLink, FormsModule],
  templateUrl: './historical-season.component.html',
  styleUrl: './historical-season.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoricalSeasonComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);

  readonly seasonID = signal<number>(0);
  readonly season = signal<HistoricalSeasonDto | null>(null);
  readonly loading = signal<boolean>(true);
  readonly activeTab = signal<'teams' | 'stats'>('teams');
  readonly selectedPhase = signal<'all' | 'regular' | 'playoffs'>('all');
  readonly playerFilter = signal<string>('');

  readonly filteredPlayerStats = computed(() => {
    const s = this.season();
    if (!s) return [];
    let list = s.playerStats;

    const phase = this.selectedPhase();
    if (phase === 'regular') list = list.filter(p => !p.isPlayoffs);
    else if (phase === 'playoffs') list = list.filter(p => p.isPlayoffs);

    const query = this.playerFilter().trim().toLowerCase();
    if (query) {
      list = list.filter(p => p.gamertag.toLowerCase().includes(query) || (p.teamName?.toLowerCase().includes(query) ?? false));
    }
    return list;
  });

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('seasonID'));
    this.seasonID.set(id);

    this.http.get<HistoricalSeasonDto>(`api/historical/season/${id}`)
      .subscribe({
        next: (data) => {
          this.season.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        }
      });
  }

  setTab(tab: 'teams' | 'stats'): void {
    this.activeTab.set(tab);
  }

  setPhase(phase: 'all' | 'regular' | 'playoffs'): void {
    this.selectedPhase.set(phase);
  }
}
