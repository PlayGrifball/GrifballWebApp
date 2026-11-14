import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LateLeagueQueueStore } from './lateLeagueQueue.store';

/**
 * The Late League queue, on the home page and the Late League page: how many players are in it, live, and Join / Leave for a signed-in
 * player with a gamertag, on the Discord buttons' rules (the API says why it refused).
 *
 * Only in the browser: the status is the viewer's own, so a page rendered on the server (and maybe cached)
 * leaves it out, and the hub connection is a WebSocket.
 */
@Component({
  selector: 'app-late-league-queue',
  templateUrl: 'lateLeagueQueue.component.html',
  styleUrl: 'lateLeagueQueue.component.scss',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [LateLeagueQueueStore],
})
export class LateLeagueQueueComponent {
  private readonly store = inject(LateLeagueQueueStore);

  /** Link to the Late League page: not on that page itself. */
  readonly showLink = input(true);

  readonly status = this.store.status;
  /** The status could not be loaded. */
  readonly failed = this.store.failed;
  /** A join or leave is under way. */
  readonly busy = this.store.busy;
  /** What came of the last join or leave. */
  readonly message = this.store.message;

  join(): void {
    this.store.join();
  }

  leave(): void {
    this.store.leave();
  }
}
