import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { OnDemandJobStatusDto, XboxAccountDto } from '@api/models';
import { XboxAccountsService } from '@api/services/xbox-accounts.service';
import { errorText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';
import { Subscription, switchMap, takeWhile, timer } from 'rxjs';

/** How often a queued or running pull of the account's games is looked at again. */
export const PULL_POLL_MS = 3000;

/** What the page shows: loading, the account, no such account, or the request failed. */
export type XboxAccountState = 'loading' | 'loaded' | 'notFound' | 'error';

/**
 * An Xbox account and its games, a page at a time, and the pull of its recent games from Halo (a job on the worker,
 * watched until it's done, then the newest games read again). One per page (the component provides it).
 */
@Injectable()
export class XboxAccountStore {
  private readonly api = inject(XboxAccountsService);
  private readonly destroyRef = inject(DestroyRef);

  /** The account's XUID: the component's input. */
  readonly xuid = new StoreInput<string>();

  private readonly _account = signal<XboxAccountDto | null>(null);
  readonly account = this._account.asReadonly();
  private readonly _state = signal<XboxAccountState>('loading');
  readonly state = this._state.asReadonly();
  private readonly _page = signal(1);
  readonly page = this._page.asReadonly();

  readonly hasOlder = computed(() => {
    const account = this.account();
    return !!account && account.page * account.pageSize < account.gameCount;
  });
  readonly hasNewer = computed(() => (this.account()?.page ?? 1) > 1);

  /** The pull of the account's games from Halo (a job on the worker): where it is, null until known. */
  private readonly _pull = signal<OnDemandJobStatusDto | null>(null);
  readonly pull = this._pull.asReadonly();
  /** Queued or running: the button waits, and the page looks again every few seconds. */
  readonly pulling = computed(() => this.pull()?.state === 'Queued' || this.pull()?.state === 'Running');
  private readonly _pullError = signal<string | null>(null);
  readonly pullError = this._pullError.asReadonly();
  private pullWatch?: Subscription;

  constructor() {
    // A new account or page cancels the previous request, and so does leaving the page
    effect(onCleanup => {
      const xuid = this.xuid.value();
      if (xuid === undefined)
        return;
      const request = this.load(xuid, this.page());
      onCleanup(() => request.unsubscribe());
    });
    // Where the account's pull is, for each account shown
    effect(onCleanup => {
      const xuid = this.xuid.value();
      if (xuid === undefined)
        return;
      const request = this.getPullStatus(xuid);
      onCleanup(() => {
        request.unsubscribe();
        this.pullWatch?.unsubscribe();
      });
    });
  }

  /** Queues a pull of the account's recent games from Halo Infinite: the worker runs it within a few seconds. */
  pullGames(): void {
    this._pullError.set(null);
    this.api.pullXboxAccount({ xboxUserID: Number(this.xuid.value()) })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.showPull(status),
        error: (error: HttpErrorResponse) => this._pullError.set(errorText(error, "Couldn't start the pull.")),
      });
  }

  older(): void {
    this._page.update(p => p + 1);
  }

  newer(): void {
    this._page.update(p => Math.max(1, p - 1));
  }

  private getPullStatus(xuid: string): Subscription {
    this.pullWatch?.unsubscribe();
    this._pull.set(null);
    this._pullError.set(null);
    return this.api.getXboxAccountPullStatus({ xboxUserID: Number(xuid) }).subscribe({
      next: status => this.showPull(status),
      error: () => this._pull.set(null),
    });
  }

  private showPull(status: OnDemandJobStatusDto): void {
    this._pull.set(status);
    if (status.state === 'Idle')
      return;
    // Look again every few seconds until it's done, then read the games it found (from the newest)
    this.pullWatch?.unsubscribe();
    this.pullWatch = timer(PULL_POLL_MS, PULL_POLL_MS).pipe(
      switchMap(() => this.api.getXboxAccountPullStatus({ xboxUserID: Number(this.xuid.value()) })),
      takeWhile(next => next.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => {
        this._pull.set(next);
        if (next.state === 'Idle')
          this.reload();
      },
      error: () => this._pullError.set("Couldn't tell whether the pull finished. Reload the page to check."),
    });
  }

  /** The newest games again: page 1, read again even when it's the page shown. */
  private reload(): void {
    if (this.page() !== 1) {
      this._page.set(1);
      return;
    }
    this.load(this.xuid.value()!, 1);
  }

  private load(xuid: string, page: number): Subscription {
    this._state.set('loading');
    return this.api.getXboxAccount({ xboxUserID: Number(xuid), page }).subscribe({
      next: account => {
        this._account.set(account);
        this._state.set('loaded');
      },
      error: (error: HttpErrorResponse) => {
        this._account.set(null);
        this._state.set(error.status === 404 || error.status === 400 ? 'notFound' : 'error');
      },
    });
  }
}
