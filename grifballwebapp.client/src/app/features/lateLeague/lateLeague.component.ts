import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { LateLeagueService } from '@api/services/late-league.service';
import { LateLeagueQueueComponent } from './lateLeagueQueue/lateLeagueQueue.component';
import type { MatchmakingPlayerStatDto } from '@api/models';

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
  private api = inject(LateLeagueService);
  /** The route's season; none for Late League as a whole. */
  readonly matchmakingSeasonID = input<string | undefined>(undefined);

  /** The route's season, if it names one. */
  private readonly seasonID = computed(() => {
    const id = Number(this.matchmakingSeasonID());
    return id > 0 ? id : undefined;
  });

  /** A season's players (with a season); the request still on its way is cancelled when it changes. */
  private readonly season = rxResource({
    params: () => this.seasonID(),
    stream: ({ params: matchmakingSeasonID }) => this.api.getLateLeagueSeason({ matchmakingSeasonID }),
  });
  /** Late League as a whole (without one): the queue's players and the archive's seasons, each on its own so one that
   * fails doesn't take the other with it. */
  private readonly overview = rxResource({
    params: () => this.seasonID() === undefined ? true : undefined,
    stream: () => forkJoin({
      queue: this.api.getLateLeagueQueue().pipe(catchError(() => of(null))),
      seasons: this.api.getLateLeagueSeasons().pipe(catchError(() => of(null))),
    }),
  });

  readonly queue = computed(() => this.overview.hasValue() ? this.overview.value().queue : null);
  readonly seasons = computed(() => this.overview.hasValue() ? this.overview.value().seasons : null);
  readonly detail = computed(() => this.season.hasValue() ? this.season.value() : null);
  /** What the page shows has loaded, or failed to. */
  readonly loaded = computed(() => !this.season.isLoading() && !this.overview.isLoading());

  /** Some of the players' games were decided by vote: the table says what those count for. */
  anyByVote(players: MatchmakingPlayerStatDto[]): boolean {
    return players.some(p => p.gamesDecidedByVote > 0);
  }
}
