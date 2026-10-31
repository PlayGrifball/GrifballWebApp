import { HttpClient } from '@angular/common/http';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

export interface HistoricalChampionRosterDto {
  userID: number;
  gamertag: string;
  isCaptain: boolean;
}

export interface HistoricalTeamChampionshipDto {
  seasonID: number;
  seasonName: string;
  year?: number | null;
  leagueName: string;
  gameVersionName?: string | null;
  tier: string;
  roster: HistoricalChampionRosterDto[];
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

export interface HistoricalTeamSeasonAppearanceDto {
  seasonID: number;
  seasonName: string;
  year?: number | null;
  leagueName: string;
  gameVersionName: string;
  gamesPlayed: number;
  wins: number;
  losses: number;
  goals: number;
  wonChampionship: boolean;
  roster: HistoricalTeamPlayerDto[];
}

export interface HistoricalTeamAllTimePlayerDto {
  userID: number;
  gamertag: string;
  seasonsPlayed: number;
  gamesPlayed: number;
  goals: number;
  championshipsWon: number;
}

export interface HistoricalTeamDetailDto {
  teamName: string;
  teamSlug: string;
  totalChampionships: number;
  championships: HistoricalTeamChampionshipDto[];
  seasons: HistoricalTeamSeasonAppearanceDto[];
  allTimePlayers: HistoricalTeamAllTimePlayerDto[];
}

@Component({
  selector: 'app-historical-team',
  imports: [RouterLink],
  templateUrl: './historical-team.component.html',
  styleUrl: './historical-team.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoricalTeamComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);

  readonly teamSlug = signal<string>('');
  readonly team = signal<HistoricalTeamDetailDto | null>(null);
  readonly loading = signal<boolean>(true);

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('teamSlug') ?? '';
    this.teamSlug.set(slug);

    this.http.get<HistoricalTeamDetailDto>(`api/historical/team/${slug}`)
      .subscribe({
        next: (data) => {
          this.team.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        }
      });
  }
}
