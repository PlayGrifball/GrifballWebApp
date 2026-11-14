import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { Subscription, switchMap, takeWhile, timer } from 'rxjs';
import { XboxAccountsService } from '@api/services/xbox-accounts.service';
import type { OnDemandJobStatusDto, XboxAccountDto } from '@api/models';
import { AccountService } from '@core/auth/account.service';
import { describePage } from '@shared/pageMeta';
import { errorText } from '@shared/errorText';

/** How often a queued or running pull of the account's games is looked at again. */
export const PULL_POLL_MS = 3000;

/** What the page shows: loading, the account, no such account, or the request failed. */
export type XboxAccountState = 'loading' | 'loaded' | 'notFound' | 'error';

/**
 * An Xbox account's own page: its gamertag and the Halo games the site saved that it played in, newest first, each linking to
 * its game page. Halo's data only, nothing of the league: the profile has that, linked when a user holds the account; otherwise
 * no account was created for it. Linked from a game page's players who have no account. A player (or staff) can pull the
 * account's recent games from Halo, as a season match's players' are pulled: a job on the worker, watched until it's done.
 */
@Component({
  selector: 'app-xbox-account',
  imports: [RouterModule, DatePipe],
  templateUrl: './xboxAccount.component.html',
  styleUrl: './xboxAccount.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class XboxAccountComponent {
  private readonly api = inject(XboxAccountsService);
  private readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);

  /** The account's XUID (the route's :xuid). */
  readonly xuid = input.required<string>();

  readonly account = signal<XboxAccountDto | null>(null);
  readonly state = signal<XboxAccountState>('loading');
  readonly page = signal(1);

  readonly hasOlder = computed(() => {
    const account = this.account();
    return !!account && account.page * account.pageSize < account.gameCount;
  });
  readonly hasNewer = computed(() => (this.account()?.page ?? 1) > 1);

  /** Whether the user may pull the account's games: any player, as staff may (the server allows the same). */
  readonly canPull = computed(() => this.accountService.isPlayer() || this.accountService.isEventOrganizer() || this.accountService.isSysAdmin());
  /** The pull of the account's games from Halo (a job on the worker): where it is, null until known. */
  readonly pull = signal<OnDemandJobStatusDto | null>(null);
  /** Queued or running: the button waits, and the page looks again every few seconds. */
  readonly pulling = computed(() => this.pull()?.state === 'Queued' || this.pull()?.state === 'Running');
  readonly pullError = signal<string | null>(null);
  private pullWatch?: Subscription;

  constructor() {
    describePage(() => {
      const account = this.account();
      return account ? { title: account.gamertag, description: `${account.gamertag}'s Halo Infinite Grifball games: ${account.gameCount} saved.` } : null;
    });
    // A new account or page cancels the previous request, and so does leaving the page
    effect(onCleanup => {
      const request = this.load(this.xuid(), this.page());
      onCleanup(() => request.unsubscribe());
    });
    // Where the account's pull is, for each account shown
    effect(onCleanup => {
      const request = this.getPullStatus(this.xuid());
      onCleanup(() => {
        request.unsubscribe();
        this.pullWatch?.unsubscribe();
      });
    });
  }

  /** Queues a pull of the account's recent games from Halo Infinite: the worker runs it within a few seconds. */
  pullGames(): void {
    this.pullError.set(null);
    this.api.pullXboxAccount({ xboxUserID: Number(this.xuid()) })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.showPull(status),
        error: (error: HttpErrorResponse) => this.pullError.set(errorText(error, "Couldn't start the pull.")),
      });
  }

  private getPullStatus(xuid: string): Subscription {
    this.pullWatch?.unsubscribe();
    this.pull.set(null);
    this.pullError.set(null);
    return this.api.getXboxAccountPullStatus({ xboxUserID: Number(xuid) }).subscribe({
      next: status => this.showPull(status),
      error: () => this.pull.set(null),
    });
  }

  private showPull(status: OnDemandJobStatusDto): void {
    this.pull.set(status);
    if (status.state === 'Idle')
      return;
    // Look again every few seconds until it's done, then read the games it found (from the newest)
    this.pullWatch?.unsubscribe();
    this.pullWatch = timer(PULL_POLL_MS, PULL_POLL_MS).pipe(
      switchMap(() => this.api.getXboxAccountPullStatus({ xboxUserID: Number(this.xuid()) })),
      takeWhile(next => next.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => {
        this.pull.set(next);
        if (next.state === 'Idle')
          this.reload();
      },
      error: () => this.pullError.set("Couldn't tell whether the pull finished. Reload the page to check."),
    });
  }

  /** The newest games again: page 1, read again even when it's the page shown. */
  private reload(): void {
    if (this.page() !== 1) {
      this.page.set(1);
      return;
    }
    this.load(this.xuid(), 1);
  }

  older(): void {
    this.page.update(p => p + 1);
  }

  newer(): void {
    this.page.update(p => Math.max(1, p - 1));
  }

  private load(xuid: string, page: number): Subscription {
    this.state.set('loading');
    return this.api.getXboxAccount({ xboxUserID: Number(xuid), page }).subscribe({
      next: account => {
        this.account.set(account);
        this.state.set('loaded');
      },
      error: (error: HttpErrorResponse) => {
        this.account.set(null);
        this.state.set(error.status === 404 || error.status === 400 ? 'notFound' : 'error');
      },
    });
  }
}
