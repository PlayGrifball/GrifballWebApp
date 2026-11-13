import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, InjectionToken, OnInit, PLATFORM_ID, computed, inject, input, signal } from '@angular/core';
import { Location, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { errorText } from '../errorText';
import { ExternalLoginsService } from '@api/services/external-logins.service';
import type { ExternalLoginDto, ExternalProviderDto, LinkedLoginsDto } from '@api/models';

export type ExternalLogin = ExternalLoginDto;
export type ExternalProvider = ExternalProviderDto;
export type LinkedLogins = LinkedLoginsDto;

/** Full-page navigation, swapped out in tests. */
export const BROWSER_NAVIGATE = new InjectionToken<(url: string) => void>('BROWSER_NAVIGATE', {
  providedIn: 'root',
  factory: () => (url: string) => window.location.assign(url),
});

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
})
export class LinkedAccountsComponent implements OnInit {
  private externalLogins = inject(ExternalLoginsService);
  private route = inject(ActivatedRoute);
  private location = inject(Location);
  private navigate = inject(BROWSER_NAVIGATE);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Set for a sysadmin managing another user; unset for the signed-in user's own accounts. */
  userID = input<number>();

  readonly data = signal<LinkedLogins | undefined>(undefined);
  readonly message = signal<string | null>(null);
  readonly busy = signal(false);
  readonly canUnlink = computed(() => {
    const d = this.data();
    return !!d && (d.hasPassword || d.logins.length > 1);
  });

  ngOnInit(): void {
    if (this.userID() === undefined && this.route.snapshot.queryParamMap.get('linkCallback') === 'true') {
      // Only in the browser: on the server the link would be added there (its cookies never reaching the
      // browser) and then again by the browser. The server draws the list empty; the browser fills it.
      if (this.isBrowser)
        this.finishLink();
    } else {
      this.load();
    }
  }

  load(): void {
    const id = this.userID();
    const call = id === undefined ? this.externalLogins.getMyExternalLogins() : this.externalLogins.getUserExternalLogins({ userID: id });
    call.subscribe({
      next: r => this.data.set({ ...r, providers: r.providers ?? [] }),
      error: (e: HttpErrorResponse) => this.message.set(errorText(e, 'Failed to load linked accounts')),
    });
  }

  link(provider: ExternalProvider): void {
    this.busy.set(true);
    this.externalLogins.startExternalLoginLink({ provider: provider.name }).subscribe({
      next: r => this.navigate('/api/external-logins/link?provider=' + encodeURIComponent(provider.name) + '&token=' + encodeURIComponent(r.token)),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.message.set(errorText(e, 'Failed to link ' + provider.displayName));
      },
    });
  }

  unlink(login: ExternalLogin): void {
    const id = this.userID();
    const body = { loginProvider: login.loginProvider, providerKey: login.providerKey };
    const call = id === undefined ? this.externalLogins.unlinkExternalLogin({ body }) : this.externalLogins.removeUserExternalLogin({ userID: id, body });
    this.busy.set(true);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.message.set('Unlinked ' + login.providerDisplayName);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.message.set(errorText(e, 'Failed to unlink ' + login.providerDisplayName));
      },
    });
  }

  /** Back from the provider: add the login it returned to this account, then drop the query parameter. */
  private finishLink(): void {
    this.location.replaceState(this.location.path().split('?')[0]);
    this.externalLogins.externalLoginLinkCallback().subscribe({
      next: r => {
        this.message.set('Linked ' + r.providerDisplayName);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.message.set(errorText(e, 'Failed to link the account'));
        this.load();
      },
    });
  }
}
