import { ChangeDetectionStrategy, Component, OnInit, PLATFORM_ID, computed, inject, input } from '@angular/core';
import { Location, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import type { ExternalLoginDto, ExternalProviderDto, LinkedLoginsDto } from '@api/models';
import { LinkedAccountsStore } from './linkedAccounts.store';

export type ExternalLogin = ExternalLoginDto;
export type ExternalProvider = ExternalProviderDto;
export type LinkedLogins = LinkedLoginsDto;

/**
 * External logins (e.g. Discord) of the signed-in user, who can link and unlink them,
 * or, given a userID, of that user for a sysadmin, who can remove them.
 * A login is never removable when it is the account's only way to sign in.
 */
@Component({
  selector: 'app-linked-accounts',
  imports: [MatButtonModule],
  templateUrl: './linkedAccounts.component.html',
  styleUrl: './linkedAccounts.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [LinkedAccountsStore],
})
export class LinkedAccountsComponent implements OnInit {
  private readonly store = inject(LinkedAccountsStore);
  private route = inject(ActivatedRoute);
  private location = inject(Location);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Set for a sysadmin managing another user; unset for the signed-in user's own accounts. */
  userID = input<number>();

  readonly data = this.store.data;
  readonly message = this.store.message;
  readonly busy = this.store.busy;
  readonly canUnlink = computed(() => {
    const d = this.data();
    return !!d && (d.hasPassword || d.logins.length > 1);
  });

  constructor() {
    this.store.userID.use(computed(() => this.userID() ?? null));
  }

  ngOnInit(): void {
    if (this.userID() === undefined && this.route.snapshot.queryParamMap.get('linkCallback') === 'true') {
      // Only in the browser: on the server the link would be added there (its cookies never reaching the
      // browser) and then again by the browser. The server draws the list empty; the browser fills it.
      if (this.isBrowser) {
        // Back from the provider: the query parameter goes, and the login it returned is added
        this.location.replaceState(this.location.path().split('?')[0]);
        this.store.finishLink();
      }
    } else {
      this.store.load();
    }
  }

  link(provider: ExternalProvider): void {
    this.store.link(provider);
  }

  unlink(login: ExternalLogin): void {
    this.store.unlink(login);
  }
}
