import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { MergeRequest } from '@api/models';
import { UserManagementService } from '@api/services/user-management.service';
import { errorText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';
import { map } from 'rxjs';
import { parseUser } from '../userResponseDto';

/** The two users of a merge, looked up by ID, and merging them. One per page (the component provides it). */
@Injectable()
export class MergeUserStore {
  private readonly userManagement = inject(UserManagementService);

  /** The user merged away, and the one kept (0: none chosen): the component's fields. */
  readonly fromID = new StoreInput<number>();
  readonly toID = new StoreInput<number>();

  // An undefined userID keeps the resource idle, so no request is made until an ID is chosen.
  private readonly from = rxResource({
    params: () => this.fromID.value() || undefined,
    stream: ({ params: userID }) => this.userManagement.getUser({ userID }).pipe(map(parseUser)),
  });
  private readonly to = rxResource({
    params: () => this.toID.value() || undefined,
    stream: ({ params: userID }) => this.userManagement.getUser({ userID }).pipe(map(parseUser)),
  });

  // value() throws while a resource is in the error state, so the users are read through these.
  readonly fromUser = computed(() => this.from.hasValue() ? this.from.value() : undefined);
  readonly toUser = computed(() => this.to.hasValue() ? this.to.value() : undefined);
  /** Why a lookup failed (a 404: no such user); undefined when it didn't. */
  readonly fromError = computed(() => this.from.error());
  readonly toError = computed(() => this.to.error());

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
