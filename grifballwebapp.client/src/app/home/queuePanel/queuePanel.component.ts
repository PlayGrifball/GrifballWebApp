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
    connection.start().catch(err => console.log('Error while starting the queue connection: ' + err));
    inject(DestroyRef).onDestroy(() => void connection.stop());

    inject(AuthSession).changed$.pipe(takeUntilDestroyed()).subscribe(() => {
      this.message.set(null);
      this.load();
    });
    this.load();
  }

  load(): void {
    this.http.get<QueueStatusDto>('/api/Queue/Status').subscribe({
      next: status => {
        this.status.set(status);
        this.failed.set(false);
      },
      error: () => this.failed.set(this.status() === null),
    });
  }

  /**
   * The hub's news: the queue's size. A signed-in player's own place in it may have changed too (matched,
   * or put back when a match was canceled), so theirs is loaded again.
   */
  counted(count: number): void {
    const status = this.status();
    if (!status || status.count === count)
      return;
    this.status.set({ ...status, count });
    if (status.signedIn)
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
    request.subscribe({
      next: status => {
        this.status.set(status);
        this.message.set(done);
        this.busy.set(false);
      },
      error: (err: unknown) => {
        // A refusal says why (the Discord buttons' message); the status was not what the page showed
        const reason = err instanceof HttpErrorResponse && typeof err.error === 'string' && err.error ? err.error : null;
        this.message.set(reason ?? 'Something went wrong, try again');
        this.busy.set(false);
        this.load();
      },
    });
  }
}
