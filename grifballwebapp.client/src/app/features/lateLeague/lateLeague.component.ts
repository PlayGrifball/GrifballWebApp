import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LateLeagueQueueComponent } from './lateLeagueQueue/lateLeagueQueue.component';
import type { MatchmakingPlayerStatDto } from '@api/models';
import { LateLeagueStore } from './lateLeague.store';

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
  providers: [LateLeagueStore],
})
export class LateLeagueComponent {
  private readonly store = inject(LateLeagueStore);
  /** The route's season; none for Late League as a whole. */
  readonly matchmakingSeasonID = input<string | undefined>(undefined);

  /** The route's season, if it names one (null: none). */
  private readonly seasonID = computed(() => {
    const id = Number(this.matchmakingSeasonID());
    return id > 0 ? id : null;
  });

  readonly queue = this.store.queue;
  readonly seasons = this.store.seasons;
  readonly detail = this.store.detail;
  /** What the page shows has loaded, or failed to. */
  readonly loaded = this.store.loaded;

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }

  /** Some of the players' games were decided by vote: the table says what those count for. */
  anyByVote(players: MatchmakingPlayerStatDto[]): boolean {
    return players.some(p => p.gamesDecidedByVote > 0);
  }
}
