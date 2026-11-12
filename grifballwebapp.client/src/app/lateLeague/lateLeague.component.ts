import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of, Subscription } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import { LateLeagueQueueComponent } from './lateLeagueQueue/lateLeagueQueue.component';
import { MatchmakingPlayerStatDto, MatchmakingQueueDto, MatchmakingSeasonDetailDto, MatchmakingSeasonDto } from '@api/models';

/**
 * Late League: the Discord queue's matchmaking, random skill-matched sides every game, so no teams, standings or
 * champions. Without a season (/late-league) it shows the queue's players as it is played now, then the archive's
 * Late League seasons; with one (/late-league/:matchmakingSeasonID) that season's players.
 */
@Component({
  selector: 'app-late-league',
  imports: [RouterLink, DatePipe, NgTemplateOutlet, LateLeagueQueueComponent],
  templateUrl: './lateLeague.component.html',
  styleUrl: './lateLeague.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LateLeagueComponent {
  private api = inject(ApiClientService);
  /** The route's season; none for Late League as a whole. */
  readonly matchmakingSeasonID = input<string | undefined>(undefined);

  readonly queue = signal<MatchmakingQueueDto | null>(null);
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
      const done = () => this.loaded.set(true);
      // Each part on its own: one that fails doesn't take the other with it
      this.request = id > 0
        ? this.api.getLateLeagueSeason(id).subscribe({ next: d => this.detail.set(d), error: done, complete: done })
        : forkJoin({
            queue: this.api.getLateLeagueQueue().pipe(catchError(() => of(null))),
            seasons: this.api.getLateLeagueSeasons().pipe(catchError(() => of(null))),
          }).subscribe(({ queue, seasons }) => {
            this.queue.set(queue);
            this.seasons.set(seasons);
            done();
          });
    });
  }

  /** Some of the players' games were decided by vote: the table says what those count for. */
  anyByVote(players: MatchmakingPlayerStatDto[]): boolean {
    return players.some(p => p.gamesDecidedByVote > 0);
  }
}
