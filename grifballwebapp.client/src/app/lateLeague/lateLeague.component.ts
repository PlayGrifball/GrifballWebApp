import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import type { MatchmakingSeasonDetailDto, MatchmakingSeasonDto } from '../api/dtos/lateLeagueDto';

/**
 * Late League: matchmaking, random skill-matched sides every game, so no teams, standings or champions. Without a season
 * (/late-league) it lists the matchmaking seasons; with one (/late-league/:matchmakingSeasonID) it shows that season's
 * players.
 */
@Component({
  selector: 'app-late-league',
  imports: [RouterLink, DatePipe],
  templateUrl: './lateLeague.component.html',
  styleUrl: './lateLeague.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LateLeagueComponent {
  private api = inject(ApiClientService);
  /** The route's season; none for the list. */
  readonly matchmakingSeasonID = input<string | undefined>(undefined);

  readonly seasons = signal<MatchmakingSeasonDto[] | null>(null);
  readonly detail = signal<MatchmakingSeasonDetailDto | null>(null);
  readonly loaded = signal(false);
  private request?: Subscription;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
    effect(() => {
      const id = Number(this.matchmakingSeasonID());
      this.request?.unsubscribe();
      this.loaded.set(false);
      this.detail.set(null);
      const done = { error: () => this.loaded.set(true), complete: () => this.loaded.set(true) };
      this.request = id > 0
        ? this.api.getLateLeagueSeason(id).subscribe({ next: d => this.detail.set(d), ...done })
        : this.api.getLateLeagueSeasons().subscribe({ next: s => this.seasons.set(s), ...done });
    });
  }
}
