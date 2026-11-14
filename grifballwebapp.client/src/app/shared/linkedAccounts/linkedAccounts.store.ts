import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, InjectionToken, signal } from '@angular/core';
import type { ExternalLoginDto, ExternalProviderDto, LinkedLoginsDto } from '@api/models';
import { ExternalLoginsService } from '@api/services/external-logins.service';
import { errorText } from '../errorText';
import { StoreInput } from '../storeInput';

/** Full-page navigation, swapped out in tests. */
export const BROWSER_NAVIGATE = new InjectionToken<(url: string) => void>('BROWSER_NAVIGATE', {
  providedIn: 'root',
  factory: () => (url: string) => window.location.assign(url),
});

/**
 * A user's external logins: the signed-in user's own (null), or another user's for a sysadmin; linking one (off to
 * the provider and back), and unlinking or removing one. One per panel (the component provides it).
 */
@Injectable()
export class LinkedAccountsStore {
  private readonly externalLogins = inject(ExternalLoginsService);
  private readonly navigate = inject(BROWSER_NAVIGATE);

  /** Whose logins (null: the signed-in user's own): the component's input. */
  readonly userID = new StoreInput<number | null>();

  private readonly _data = signal<LinkedLoginsDto | undefined>(undefined);
  readonly data = this._data.asReadonly();
  private readonly _message = signal<string | null>(null);
  readonly message = this._message.asReadonly();
  private readonly _busy = signal(false);
  readonly busy = this._busy.asReadonly();

  load(): void {
    const id = this.userID.value() ?? null;
    const call = id === null ? this.externalLogins.getMyExternalLogins() : this.externalLogins.getUserExternalLogins({ userID: id });
    call.subscribe({
      next: r => this._data.set({ ...r, providers: r.providers ?? [] }),
      error: (e: HttpErrorResponse) => this._message.set(errorText(e, 'Failed to load linked accounts')),
    });
  }

  /** Off to the provider to link a login, with a token the server gives for it. */
  link(provider: ExternalProviderDto): void {
    this._busy.set(true);
    this.externalLogins.startExternalLoginLink({ provider: provider.name }).subscribe({
      next: r => this.navigate('/api/external-logins/link?provider=' + encodeURIComponent(provider.name) + '&token=' + encodeURIComponent(r.token)),
      error: (e: HttpErrorResponse) => {
        this._busy.set(false);
        this._message.set(errorText(e, 'Failed to link ' + provider.displayName));
      },
    });
  }

  unlink(login: ExternalLoginDto): void {
    const id = this.userID.value() ?? null;
    const body = { loginProvider: login.loginProvider, providerKey: login.providerKey };
    const call = id === null ? this.externalLogins.unlinkExternalLogin({ body }) : this.externalLogins.removeUserExternalLogin({ userID: id, body });
    this._busy.set(true);
    call.subscribe({
      next: () => {
        this._busy.set(false);
        this._message.set('Unlinked ' + login.providerDisplayName);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this._busy.set(false);
        this._message.set(errorText(e, 'Failed to unlink ' + login.providerDisplayName));
      },
    });
  }

  /** Back from the provider: adds the login it returned to this account, then loads the list. */
  finishLink(): void {
    this.externalLogins.externalLoginLinkCallback().subscribe({
      next: r => {
        this._message.set('Linked ' + r.providerDisplayName);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this._message.set(errorText(e, 'Failed to link the account'));
        this.load();
      },
    });
  }
}
