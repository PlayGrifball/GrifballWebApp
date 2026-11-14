import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { AccessTokenDto } from '@api/models';
import { AccessTokensService } from '@api/services/access-tokens.service';
import { problemText } from '@shared/errorText';

/** The signed-in user's access tokens: the list, making one (shown once) and revoking one. One per panel (the component provides it). */
@Injectable()
export class AccessTokensStore {
  private readonly api = inject(AccessTokensService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly tokens = rxResource({
    params: () => true,
    stream: () => this.api.getAccessTokens(),
  });
  readonly loaded = computed(() => this.tokens.hasValue());
  readonly rows = computed<AccessTokenDto[]>(() => this.tokens.hasValue() ? this.tokens.value() : []);
  readonly failed = computed(() => this.tokens.status() === 'error');

  private readonly _creating = signal(false);
  readonly creating = this._creating.asReadonly();
  /** The token just made, shown this once. */
  private readonly _created = signal<string | null>(null);
  readonly created = this._created.asReadonly();

  /** Makes a token lasting `days`; `made` runs once it is (the list then loads again). Nothing while one is being made. */
  create(name: string, days: number, made?: () => void): void {
    if (this.creating())
      return;
    this._creating.set(true);
    this.api.createAccessToken({ body: { name, days } }).subscribe({
      next: token => {
        this._creating.set(false);
        this._created.set(token.token);
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
