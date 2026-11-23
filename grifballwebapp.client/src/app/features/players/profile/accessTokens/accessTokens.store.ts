import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { AccessTokenDto, AccessTokenScope, CreatedAccessTokenDto } from '@api/models';
import { AccessTokensService } from '@api/services/access-tokens.service';
import { problemText } from '@shared/errorText';
import { resourceView } from '@shared/resourceView';

/** The signed-in user's access tokens: the list, making one (shown once) and revoking one. One per panel (the component provides it). */
@Injectable()
export class AccessTokensStore {
  private readonly api = inject(AccessTokensService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly tokens = resourceView(rxResource({
    params: () => true,
    stream: () => this.api.getAccessTokens(),
  }), []);
  readonly loaded = this.tokens.hasValue;
  readonly rows = this.tokens.value;
  readonly failed = this.tokens.failed;

  private readonly _creating = signal(false);
  readonly creating = this._creating.asReadonly();
  /** The token just made (null once nothing is shown): its own token and scope, which always arrive together, so one
   * signal holds the whole DTO instead of two that could drift apart. */
  private readonly _createdToken = signal<CreatedAccessTokenDto | null>(null);
  /** The token itself, shown this once. */
  readonly created = computed(() => this._createdToken()?.token ?? null);
  /** The just-made token's scope: a new token can't change it later. */
  readonly createdScope = computed(() => this._createdToken()?.accessToken.scope ?? null);

  /**
   * Makes a token lasting `days`, with the chosen `scope` and `readOnly` flag; `made` runs once it is (the list then
   * loads again). Nothing while one is being made.
   */
  create(name: string, days: number, scope: AccessTokenScope, readOnly: boolean, made?: () => void): void {
    if (this.creating())
      return;
    this._creating.set(true);
    this.api.createAccessToken({ body: { name, days, scope, readOnly } }).subscribe({
      next: token => {
        this._creating.set(false);
        this._createdToken.set(token);
        this.tokens.reload();
        made?.();
      },
      error: (error: HttpErrorResponse) => {
        this._creating.set(false);
        this.snackBar.open(problemText(error, "Couldn't make the token."), 'Close');
      },
    });
  }

  revoke(token: AccessTokenDto): void {
    this.api.revokeAccessToken({ id: token.id }).subscribe({
      next: () => {
        this.snackBar.open(`Revoked ${token.name}.`, 'Close');
        this.tokens.reload();
      },
      error: (error: HttpErrorResponse) => this.snackBar.open(problemText(error, "Couldn't revoke the token."), 'Close'),
    });
  }
}
