import { isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { QueueLiveDto, QueueStatusDto } from '@api/models';
import { QueueService } from '@api/services/queue.service';
import { AuthSession } from '@core/auth/authSession.service';
import { liveData, liveReload } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';
import { errorText } from '@shared/errorText';
import { Observable } from 'rxjs';

/**
 * The Late League queue as the viewer sees it: its size and the viewer's own status, loaded again when either changes (live), and
 * joining or leaving it. Only in the browser: the status is the viewer's own.
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

    // The size comes with every change to the queue, for everyone: taken as it is. The viewer's own place (matched, their
    // match over, joined or left elsewhere: Discord, another tab; kicked) comes to them alone, and their status is loaded
    // again. Both are loaded again when the live connection comes back: missed meanwhile.
    liveData<QueueLiveDto>(() => [LiveTopics.queue], queue => this.counted(queue.count), () => this.load());
    liveReload(() => [LiveTopics.myQueue], () => this.load());

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

  /** The queue's size, as a change pushed it: shown in the status (false: none loaded yet, so it is loaded). */
  private counted(count: number): boolean {
    const status = this.status();
    if (!status)
      return false;
    this._status.set({ ...status, count });
    return true;
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
