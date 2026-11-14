import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterModule } from '@angular/router';
import { AccountService } from '@core/auth/account.service';
import { describePage } from '@shared/pageMeta';
import { XboxAccountStore } from './xboxAccount.store';

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
  providers: [XboxAccountStore],
})
export class XboxAccountComponent {
  private readonly store = inject(XboxAccountStore);
  private readonly accountService = inject(AccountService);

  /** The account's XUID (the route's :xuid). */
  readonly xuid = input.required<string>();

  readonly account = this.store.account;
  readonly state = this.store.state;
  readonly page = this.store.page;
  readonly hasOlder = this.store.hasOlder;
  readonly hasNewer = this.store.hasNewer;

  /** Whether the user may pull the account's games: any player, as staff may (the server allows the same). */
  readonly canPull = computed(() => this.accountService.isPlayer() || this.accountService.isEventOrganizer() || this.accountService.isSysAdmin());
  readonly pull = this.store.pull;
  readonly pulling = this.store.pulling;
  readonly pullError = this.store.pullError;

  constructor() {
    this.store.xuid.use(this.xuid);
    describePage(() => {
      const account = this.account();
      return account ? { title: account.gamertag, description: `${account.gamertag}'s Halo Infinite Grifball games: ${account.gameCount} saved.` } : null;
    });
  }

  pullGames(): void {
    this.store.pullGames();
  }

  older(): void {
    this.store.older();
  }

  newer(): void {
    this.store.newer();
  }
}
