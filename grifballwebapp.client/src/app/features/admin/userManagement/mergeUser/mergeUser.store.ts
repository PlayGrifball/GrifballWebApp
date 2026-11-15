import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { MergeRequest } from '@api/models';
import { UserManagementService } from '@api/services/user-management.service';
import { errorText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { map } from 'rxjs';
import { parseUser } from '../userResponseDto';
import { resourceView } from '@shared/resourceView';

/** The two users of a merge, looked up by ID, and merging them. One per page (the component provides it). */
@Injectable()
export class MergeUserStore {
  private readonly userManagement = inject(UserManagementService);

  /** The user merged away, and the one kept (0: none chosen): the component's fields. */
  readonly fromID = new StoreInput<number>();
  readonly toID = new StoreInput<number>();

  // An undefined userID keeps the resource idle, so no request is made until an ID is chosen.
  private readonly from = resourceView(rxResource({
    params: () => this.fromID.value() || undefined,
    stream: ({ params: userID }) => this.userManagement.getUser({ userID }).pipe(nullWhenNotFound(), map(parseUser)),
  }), undefined);
  private readonly to = resourceView(rxResource({
    params: () => this.toID.value() || undefined,
    stream: ({ params: userID }) => this.userManagement.getUser({ userID }).pipe(nullWhenNotFound(), map(parseUser)),
  }), undefined);

  /** The users looked up: undefined before an ID is chosen, null when there is no such user (404). */
  readonly fromUser = this.from.value;
  readonly toUser = this.to.value;
  /** A lookup failed (not a 404: that is no such user). */
  readonly fromFailed = this.from.failed;
  readonly toFailed = this.to.failed;

  // Disables the button while a merge is in flight, so a double click cannot send a second request.
  private readonly _submitting = signal(false);
  readonly submitting = this._submitting.asReadonly();
  private readonly _result = signal<{ ok: boolean; text: string } | null>(null);
  readonly result = this._result.asReadonly();

  /** Merges the users, and says how it went. */
  merge(request: MergeRequest): void {
    this._submitting.set(true);
    this._result.set(null);
    this.userManagement.mergeUser({ body: request }).subscribe({
      next: () => {
        console.log('Merged user');
        this._submitting.set(false);
        this._result.set({ ok: true, text: 'Merged user ' + request.mergeFromId + ' into ' + request.mergeToId });
      },
      error: (e: HttpErrorResponse) => {
        console.log(e);
        this._submitting.set(false);
        this._result.set({ ok: false, text: errorText(e, 'Merge failed') });
      },
    });
  }
}
