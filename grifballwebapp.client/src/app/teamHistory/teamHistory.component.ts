import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import type { SeasonInfoDto } from '../api/dtos/seasonInfoDto';
import type { PlayerDto } from '../season/team/team.component';

/**
 * A franchise's history: every season's team of one name (a team is a row of one season), newest first, with its
 * record, roster, captain and titles, and the franchise's totals.
 */
@Component({
  selector: 'app-team-history',
  imports: [RouterModule],
  templateUrl: './teamHistory.component.html',
  styleUrl: './teamHistory.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeamHistoryComponent {
  private readonly http = inject(HttpClient);

  /** The franchise's name, as the route gives it (case and surrounding spaces don't matter). */
  readonly teamName = input.required<string>();

  readonly history = signal<TeamHistoryDto | null>(null);
  readonly loaded = signal(false);
  readonly failed = signal(false);

  constructor() {
    effect(() => {
      const name = this.teamName();
      this.loaded.set(false);
      this.failed.set(false);
      this.http.get<TeamHistoryDto | null>('/api/Team/History', { params: { name } }).subscribe({
        next: history => {
          this.history.set(history ?? null);
          this.loaded.set(true);
        },
        error: () => {
          this.history.set(null);
          this.failed.set(true);
          this.loaded.set(true);
        },
      });
    });
  }

  /** The season's captain(s): drafted, or named by its title. */
  captains(season: TeamHistorySeasonDto): PlayerDto[] {
    return season.players.filter(p => p.isCaptain || p.draftCaptainOrder != null);
  }
}

/** A franchise: every season's team of one name, and its totals (TeamHistoryDto). */
export interface TeamHistoryDto {
  /** The name of its latest season's team. */
  teamName: string;
  appearances: number;
  wins: number;
  losses: number;
  /** The titles it won, across seasons and tiers. */
  championships: number;
  /** The players who played for it, each once. */
  players: number;
  /** Newest first. */
  seasons: TeamHistorySeasonDto[];
}

/** One season of a franchise: that season's team. */
export interface TeamHistorySeasonDto {
  teamID: number;
  /** The name as that season spelled it. */
  teamName: string;
  season: SeasonInfoDto;
  wins: number;
  losses: number;
  /** wins and losses are the source spreadsheet's: the season's matches are not in the app. */
  recordFromSource?: boolean;
  /** The tiers whose title the team won. */
  titles: string[];
  /** Captain first, then by draft round. */
  players: PlayerDto[];
}
