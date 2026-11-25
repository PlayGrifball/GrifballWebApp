import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import type { UserResponseDto as ApiUserResponseDto } from '@api/models';
import { UserManagementService } from '@api/services/user-management.service';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import { parseUser, UserResponseDto } from '../userResponseDto';
import { AppLog } from '@core/telemetry/appLog';

/** A user as loaded, and their lockout end exactly as the API sent it. */
export interface LoadedUser {
  user: UserResponseDto;
  rawLockoutEnd: string | null;
}

/** The user being edited: loading them and saving them. One per page (the component provides it). */
@Injectable()
export class EditUserStore {
  private readonly log = inject(AppLog);
  private readonly userManagement = inject(UserManagementService);

  /** The route's user (0: none): the component's input. */
  readonly userID = new StoreInput<number>();

  /** The user as loaded; null while loading, for no user, or when there is no such user. */
  private readonly _loaded = signal<LoadedUser | null>(null);
  readonly loaded = this._loaded.asReadonly();
  /** The user is on the way: the form is a skeleton until they arrive (or fail to). */
  private readonly _pending = signal(false);
  readonly pending = this._pending.asReadonly();

  constructor() {
    effect(() => {
      const userID = this.userID.value();
      if (userID !== undefined)
        untracked(() => this.load(userID));
    });
  }

  /** Saves the user; the answer, or the failure, is logged. */
  save(userID: number, body: ApiUserResponseDto): void {
    this.userManagement.editUser({ userID, body }).subscribe({
      next: () => this.log.info('Updated user ' + userID),
      error: result => this.log.error('Failed to update the user', result),
    });
  }

  private load(userID: number): void {
    // Nothing of the previous user stays, nor a wait for them
    this._loaded.set(null);
    this._pending.set(userID !== 0);
    if (userID === 0) {
      this.log.warn('Edit user: no user ID in the route');
      return;
    }
    this.userManagement.getUser({ userID })
      .pipe(whileCurrent(userID, this.userID.value))
      .subscribe({
        next: raw => {
          this._pending.set(false);
          const user = parseUser(raw);
          if (user)
            this._loaded.set({ user, rawLockoutEnd: raw.lockoutEnd ?? null });
        },
        error: error => {
          this._pending.set(false);
          this.log.error('Failed to load the user', error);
        },
      });
  }
}
