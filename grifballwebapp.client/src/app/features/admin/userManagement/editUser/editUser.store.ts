import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import type { UserResponseDto as ApiUserResponseDto } from '@api/models';
import { UserManagementService } from '@api/services/user-management.service';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import { parseUser, UserResponseDto } from '../userResponseDto';

/** A user as loaded, and their lockout end exactly as the API sent it. */
export interface LoadedUser {
  user: UserResponseDto;
  rawLockoutEnd: string | null;
}

/** The user being edited: loading them and saving them. One per page (the component provides it). */
@Injectable()
export class EditUserStore {
  private readonly userManagement = inject(UserManagementService);

  /** The route's user (0: none): the component's input. */
  readonly userID = new StoreInput<number>();

  /** The user as loaded; null while loading, for no user, or when there is no such user. */
  private readonly _loaded = signal<LoadedUser | null>(null);
  readonly loaded = this._loaded.asReadonly();

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
      next: () => console.log('Updated user'),
      error: result => console.log(result),
    });
  }

  private load(userID: number): void {
    // Nothing of the previous user stays
    this._loaded.set(null);
    if (userID === 0) {
      console.log('Invalid user ID');
      return;
    }
    this.userManagement.getUser({ userID })
      .pipe(whileCurrent(userID, this.userID.value))
      .subscribe({
        next: raw => {
          const user = parseUser(raw);
          if (user)
            this._loaded.set({ user, rawLockoutEnd: raw.lockoutEnd ?? null });
        },
        error: error => console.log(error),
      });
  }
}
