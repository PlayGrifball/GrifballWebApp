import { inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { UserResponseDto as ApiUserResponseDto } from '@api/models';
import { UserManagementService } from '@api/services/user-management.service';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';
import { map, tap } from 'rxjs';
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

  /** The user, loaded again for each user of the route (none for 0): another drops the request still on its way. */
  private readonly user = resourceView(rxResource({
    params: () => this.userID.value() || undefined,
    stream: ({ params: userID }) => this.userManagement.getUser({ userID }).pipe(
      map((raw): LoadedUser | null => {
        const user = parseUser(raw);
        return user ? { user, rawLockoutEnd: raw.lockoutEnd ?? null } : null;
      }),
      tap({ error: e => this.log.error('Failed to load the user', e) }),
    ),
  }), null);
  /** The user as loaded; null while loading, for no user, when there is no such user, or when it failed. */
  readonly loaded = this.user.value;
  /** The user is on the way: the form is a skeleton. */
  readonly firstLoad = this.user.firstLoad;
  /** The user could not be loaded: the page says so, rather than offering an empty form that would save over them. */
  readonly failed = this.user.failed;

  /** Saves the user; the answer, or the failure, is logged. */
  save(userID: number, body: ApiUserResponseDto): void {
    this.userManagement.editUser({ userID, body }).subscribe({
      next: () => this.log.info('Updated user ' + userID),
      error: result => this.log.error('Failed to update the user', result),
    });
  }
}
