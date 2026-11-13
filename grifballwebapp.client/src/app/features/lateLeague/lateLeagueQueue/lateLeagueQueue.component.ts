import { isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, PLATFORM_ID, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { QueueService } from '@api/services/queue.service';
import { AuthSession } from '@core/auth/authSession.service';
import { hubLifecycle } from '@core/signalr/hubLifecycle';
import { getReceiverRegister } from '@signalr/TypedSignalR.Client';
import type { QueueStatusDto } from '@api/models';
import { errorText } from '@shared/errorText';

/** The queue's hub (QueueHub): it sends the queue's size whenever it changes. */
export const QUEUE_HUB_URL = 'hub/QueueHub';

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
})
export class LateLeagueQueueComponent {
  private api = inject(QueueService);

  /** Link to the Late League page: not on that page itself. */
  readonly showLink = input(true);

  readonly status = signal<QueueStatusDto | null>(null);
  /** The status could not be loaded. */
  readonly failed = signal(false);
  /** A join or leave is under way. */
  readonly busy = signal(false);
  /** What came of the last join or leave. */
  readonly message = signal<string | null>(null);

  /**
   * Numbers each status request and join or leave: only the latest one's answer is shown, so a status read
   * before a change (or before a newer status) can't land after it and undo it.
   */
  private latest = 0;
  /** A load was asked for during a join or leave: it is made once that is done. */
  private loadAfterChange = false;

  constructor() {
    if (!isPlatformBrowser(inject(PLATFORM_ID)))
      return;

    // The size is for everyone; the connection is also the signed-in user's (the session cookie goes with
    // it), for the nudge when their own status changes, so hubLifecycle makes it again on a sign-in or out.
    // Connected (again): a size sent meanwhile was missed.
    const connection = hubLifecycle(QUEUE_HUB_URL, { name: 'Late League queue', connected: () => this.load() });
    // The hub's receiver, generated from its C# interface (IQueueHubClient)
    getReceiverRegister('IQueueHubClient').register(connection, {
      queueCount: count => Promise.resolve(this.counted(count)),
      // Matched, their match over, joined or left elsewhere (Discord, another tab), kicked
      queueStatusChanged: () => Promise.resolve(this.load()),
    });

    inject(AuthSession).changed$.pipe(takeUntilDestroyed()).subscribe(() => {
      this.message.set(null);
      this.load();
    });
    this.load();
  }

  load(): void {
    if (this.busy()) {
      this.loadAfterChange = true;
      return;
    }
    const request = ++this.latest;
    this.api.getQueueStatus().subscribe({
      next: status => {
        if (request !== this.latest)
          return;
        this.status.set(status);
        this.failed.set(false);
      },
      error: () => {
        if (request === this.latest)
          this.failed.set(this.status() === null);
      },
    });
  }

  /**
   * The hub's news: the queue's size. A player in the queue or in a match may have been matched, or put
   * back when a match was canceled, so theirs is loaded again; everyone else's status is unchanged, and
   * reloading it for every viewer on every change would be a request each for nothing.
   */
  counted(count: number): void {
    const status = this.status();
    // Not loaded yet, or the load failed: this size is news, so load (again)
    if (!status) {
      this.load();
      return;
    }
    if (status.count === count)
      return;
    this.status.set({ ...status, count });
    // A join or leave under way answers with a size that may be older than this one
    if (status.inQueue || status.inMatch || this.busy())
      this.load();
  }

  join(): void {
    this.change(this.api.joinQueue(), 'You have joined the Late League queue');
  }

  leave(): void {
    this.change(this.api.leaveQueue(), 'You have left the Late League queue');
  }

  private change(request: Observable<QueueStatusDto>, done: string): void {
    this.busy.set(true);
    this.message.set(null);
    // A status requested before now may have been read before the change
    ++this.latest;
    this.loadAfterChange = false;
    request.subscribe({
      next: status => {
        this.status.set(status);
        this.message.set(done);
        this.settle();
      },
      error: (err: HttpErrorResponse) => {
        // A refusal says why (the Discord buttons' message); the status was not what the page showed
        this.message.set(errorText(err, 'Something went wrong, try again'));
        this.loadAfterChange = true;
        this.settle();
      },
    });
  }

  private settle(): void {
    this.busy.set(false);
    if (this.loadAfterChange) {
      this.loadAfterChange = false;
      this.load();
    }
  }
}
