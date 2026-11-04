import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, PLATFORM_ID, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { AuthSession } from '../../authSession.service';
import { SignalRService } from '../../api/signalR.service';
import type { QueueStatusDto } from '../../api/dtos/queueStatusDto';

/** The queue's hub (QueueHub): it sends the queue's size whenever it changes. */
export const QUEUE_HUB_URL = 'hub/QueueHub';

/**
 * How long to wait before trying again when the hub connection fails to start, attempt after attempt (the
 * last one repeats). Automatic reconnects only cover a connection that dropped after it started.
 */
export const QUEUE_HUB_RETRY_MS = [2_000, 5_000, 10_000, 30_000] as const;

/**
 * The home page's matchmaking queue: how many players are in it, live, and Join / Leave for a signed-in
 * player with a gamertag, on the Discord buttons' rules (the API says why it refused).
 *
 * Only in the browser: the status is the viewer's own, so a page rendered on the server (and maybe cached)
 * leaves it out, and the hub connection is a WebSocket.
 */
@Component({
  selector: 'app-queue-panel',
  templateUrl: 'queuePanel.component.html',
  styleUrl: 'queuePanel.component.scss',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueuePanelComponent {
  private http = inject(HttpClient);

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

    // Anonymous (the size is public), so it needs no restart when someone signs in or out: the status does
    const connection = new HubConnectionBuilder()
      .withUrl(QUEUE_HUB_URL, SignalRService.connectionOptions)
      .withAutomaticReconnect()
      .build();
    connection.on('QueueCount', (count: number) => this.counted(count));
    // Back after a drop: whatever was sent meanwhile was missed
    connection.onreconnected(() => this.load());

    let stopped = false;
    let attempts = 0;
    let retry: ReturnType<typeof setTimeout> | undefined;
    const start = (): void => {
      connection.start()
        // A size sent between the first load and the connection was missed
        .then(() => this.load())
        .catch(err => {
          console.log('Error while starting the queue connection: ' + err);
          if (!stopped)
            retry = setTimeout(start, QUEUE_HUB_RETRY_MS[Math.min(attempts++, QUEUE_HUB_RETRY_MS.length - 1)]);
        });
    };
    start();
    inject(DestroyRef).onDestroy(() => {
      stopped = true;
      clearTimeout(retry);
      void connection.stop();
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
    this.http.get<QueueStatusDto>('/api/Queue/Status').subscribe({
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
    this.change(this.http.post<QueueStatusDto>('/api/Queue/Join', {}), 'You have joined the queue');
  }

  leave(): void {
    this.change(this.http.post<QueueStatusDto>('/api/Queue/Leave', {}), 'You have left the queue');
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
      error: (err: unknown) => {
        // A refusal says why (the Discord buttons' message); the status was not what the page showed
        const reason = err instanceof HttpErrorResponse && typeof err.error === 'string' && err.error ? err.error : null;
        this.message.set(reason ?? 'Something went wrong, try again');
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
