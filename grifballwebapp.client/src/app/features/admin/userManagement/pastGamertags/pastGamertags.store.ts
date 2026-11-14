import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { AddGamertagHistoryRequest, PastGamertagDto } from '@api/models';
import { GamertagClaimsService } from '@api/services/gamertag-claims.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { problemText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';

/** Which account to look up: by its XUID, or a user's ID. */
export interface PastGamertagsQuery {
  xboxUserID?: number;
  userID?: number;
}

/** An Xbox account's past gamertags: looking them up, removing a manual row and adding one. One per page (the component provides it). */
@Injectable()
export class PastGamertagsStore {
  private readonly api = inject(GamertagClaimsService);
  private readonly snackBar = inject(MatSnackBar);

  /** The account to look up (undefined: none yet): the component's lookup fields. */
  readonly query = new StoreInput<PastGamertagsQuery | undefined>();

  private readonly past = rxResource({
    params: () => this.query.value(),
    // 404: no such Xbox account (by that XUID or user ID).
    stream: ({ params }) => this.api.getPastGamertags(params).pipe(nullWhenNotFound()),
  });

  readonly loading = computed(() => this.past.isLoading());
  readonly failed = computed(() => this.past.status() === 'error');
  /** The account (none while loading, or if it could not be loaded). */
  readonly account = computed(() => this.past.hasValue() ? this.past.value() : null);
  readonly notFound = computed(() => this.past.status() === 'resolved' && this.account() === null);
  readonly rows = computed<PastGamertagDto[]>(() => this.account()?.pastGamertags ?? []);

  /** The row being removed: its button (and every other Remove button) waits for the answer. */
  private readonly _removing = signal<number | null>(null);
  readonly removing = this._removing.asReadonly();
  private readonly _adding = signal(false);
  readonly adding = this._adding.asReadonly();
  private readonly _addError = signal<string | null>(null);
  readonly addError = this._addError.asReadonly();

  /** Removes a manually-added row, then looks the account up again; a refusal is said in a snackbar. */
  remove(historyID: number): void {
    this._removing.set(historyID);
    this.api.removeGamertagHistory({ historyID }).subscribe({
      next: () => {
        this._removing.set(null);
        this.past.reload();
      },
      error: (error: HttpErrorResponse) => {
        this._removing.set(null);
        this.snackBar.open(problemText(error, "Couldn't remove that gamertag."), 'Close');
      },
    });
  }

  /** Adds a past gamertag; `added` runs once it is (and the account is looked up again). */
  add(request: AddGamertagHistoryRequest, added: () => void): void {
    this._adding.set(true);
    this._addError.set(null);
    this.api.addGamertagHistory({ body: request }).subscribe({
      next: () => {
        this._adding.set(false);
        added();
        this.past.reload();
      },
      error: (error: HttpErrorResponse) => {
        this._adding.set(false);
        this._addError.set(problemText(error, "Couldn't add that gamertag."));
      },
    });
  }
}
