import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { GeneratePasswordResetLinkResponseDto } from '@api/models';
import { UserManagementService } from '@api/services/user-management.service';
import { AccountService } from '@core/auth/account.service';
import { apiSortDirection } from '@shared/apiSort';
import { errorText } from '@shared/errorText';
import { PaginationParams } from '@shared/getPaginationResource';
import { liveReload } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';

/** The users table's pages, and making a user's password reset link. One per page (the component provides it). */
@Injectable()
export class UserManagementStore {
  private readonly userManagement = inject(UserManagementService);
  private readonly accountService = inject(AccountService);
  private readonly snackBar = inject(MatSnackBar);

  /** Bumped by a live change to the users: the table loads its page again, in place. */
  private readonly _changes = signal(0);
  readonly changes = this._changes.asReadonly();

  constructor() {
    liveReload(() => [LiveTopics.staffUsers], () => this._changes.update(n => n + 1));
  }

  /** A page of users, as the table asks for it (its search filter included). */
  readonly request = (params: PaginationParams) => this.userManagement.getUsers({
    pageNumber: params.pageNumber,
    pageSize: params.pageSize,
    sortDirection: apiSortDirection(params.sortDirection),
    sortColumn: params.sortColumn,
    search: params.filters?.find(f => f.column === 'search')?.value?.toString(),
  });

  /** Makes a password reset link for the user; `made` gets it. A failure is said in a snackbar. */
  generatePasswordResetLink(username: string, made: (response: GeneratePasswordResetLinkResponseDto) => void): void {
    this.accountService.generatePasswordResetLink({ username }).subscribe({
      next: response => made(response),
      error: error => this.snackBar.open(errorText(error, 'Failed to generate password reset link'), 'Close', { duration: 5000 }),
    });
  }
}
