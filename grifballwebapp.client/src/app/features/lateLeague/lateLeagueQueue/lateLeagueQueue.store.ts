import { isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { QueueStatusDto } from '@api/models';
import { QueueService } from '@api/services/queue.service';
import { AuthSession } from '@core/auth/authSession.service';
import { hubLifecycle } from '@core/signalr/hubLifecycle';
import { getReceiverRegister } from '@signalr/TypedSignalR.Client';
import { errorText } from '@shared/errorText';
import { Observable } from 'rxjs';

/** The queue's hub (QueueHub): it sends the queue's size whenever it changes. */
export const QUEUE_HUB_URL = 'hub/QueueHub';

/**
 * The Late League queue as the viewer sees it: its size, live from the queue's hub, the viewer's own status, and
 * joining or leaving it. Only in the browser: the status is the viewer's own, and the hub connection is a WebSocket.
 * One per queue panel (the component provides it).
 */
@Injectable()
export class LateLeagueQueueStore {
  private readonly api = inject(QueueService);

  private readonly _status = signal<QueueStatusDto | null>(null);
  readonly status = this._status.asReadonly();
  /** The status could not be loaded. */
  private readonly _failed = signal(false);
  readonly failed = this._failed.asReadonly();
  /** A join or leave is under way. */
  private readonly _busy = signal(false);
  readonly busy = this._busy.asReadonly();
  /** What came of the last join or leave. */
  private readonly _message = signal<string | null>(null);
  readonly message = this._message.asReadonly();

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
      this._message.set(null);
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
        this._status.set(status);
        this._failed.set(false);
      },
      error: () => {
        if (request === this.latest)
          this._failed.set(this.status() === null);
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
    this._status.set({ ...status, count });
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
    this._busy.set(true);
    this._message.set(null);
    // A status requested before now may have been read before the change
    ++this.latest;
    this.loadAfterChange = false;
    request.subscribe({
      next: status => {
        this._status.set(status);
        this._message.set(done);
        this.settle();
      },
      error: (err: HttpErrorResponse) => {
        // A refusal says why (the Discord buttons' message); the status was not what the page showed
        this._message.set(errorText(err, 'Something went wrong, try again'));
        this.loadAfterChange = true;
        this.settle();
      },
    });
  }

  private settle(): void {
    this._busy.set(false);
    if (this.loadAfterChange) {
      this.loadAfterChange = false;
      this.load();
    }
  }
}
