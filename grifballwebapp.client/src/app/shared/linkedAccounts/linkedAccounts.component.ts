import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, InjectionToken, OnInit, computed, inject, input, signal } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { errorText } from '../errorText';

export interface ExternalLogin {
  loginProvider: string;
  providerKey: string;
  providerDisplayName: string;
}

export interface ExternalProvider {
  name: string;
  displayName: string;
}

export interface LinkedLogins {
  hasPassword: boolean;
  logins: ExternalLogin[];
  providers: ExternalProvider[];
}

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
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private location = inject(Location);
  private navigate = inject(BROWSER_NAVIGATE);

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
      this.finishLink();
    } else {
      this.load();
    }
  }

  load(): void {
    const id = this.userID();
    const url = id === undefined ? '/api/ExternalLogins/Mine' : '/api/ExternalLogins/UserLogins/' + id;
    this.http.get<LinkedLogins>(url).subscribe({
      next: r => this.data.set({ ...r, providers: r.providers ?? [] }),
      error: (e: HttpErrorResponse) => this.message.set(errorText(e, 'Failed to load linked accounts')),
    });
  }

  link(provider: ExternalProvider): void {
    this.busy.set(true);
    this.http.post<{ token: string }>('/api/ExternalLogins/StartLink?provider=' + encodeURIComponent(provider.name), null).subscribe({
      next: r => this.navigate('/api/ExternalLogins/Link?provider=' + encodeURIComponent(provider.name) + '&token=' + encodeURIComponent(r.token)),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.message.set(errorText(e, 'Failed to link ' + provider.displayName));
      },
    });
  }

  unlink(login: ExternalLogin): void {
    const id = this.userID();
    const url = id === undefined ? '/api/ExternalLogins/Unlink' : '/api/ExternalLogins/RemoveUserLogin/' + id;
    this.busy.set(true);
    this.http.post(url, { loginProvider: login.loginProvider, providerKey: login.providerKey }).subscribe({
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
    this.http.post<ExternalLogin>('/api/ExternalLogins/LinkCallback', null).subscribe({
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
