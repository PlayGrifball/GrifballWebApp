import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, signal, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { parseUser, UserResponseDto } from './userResponseDto';
import type { UserResponseDto as ApiUserResponseDto } from '@api/models';
import { ToDatePipe } from '@shared/toDate.pipe';
import { Column, Filter, TableComponent } from '@shared/table/table.component';
import { SearchBoxComponent } from '@shared/searchBox/searchBox.component';
import { UserManagementStore } from './userManagement.store';

@Component({
    selector: 'app-user-management',
    imports: [
        RouterModule,
        MatButtonModule,
        MatIconModule,
        MatTooltipModule,
        TableComponent,
        SearchBoxComponent,
        ToDatePipe,
        DatePipe,
    ],
    templateUrl: './userManagement.component.html',
    styleUrl: './userManagement.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [UserManagementStore],
})
export class UserManagementComponent {
  private readonly store = inject(UserManagementStore);
  private snackBar = inject(MatSnackBar);

  public displayedColumns: string[] = ['userID', 'userName', 'lockoutEnd', 'lockoutEnabled', 'accessFailedCount', 'region', 'displayName', 'gamertag', 'discord', 'externalAuthCount', 'actions'];
  readonly request = this.store.request;
  // Rows of the user list are never null (PaginationResult<UserResponseDto> on the server).
  // A row of the users page (GET /api/users)
  readonly parseUser = (row: ApiUserResponseDto) => parseUser(row)!;

  filter = signal<string>('');
  filters = computed<Filter[]>(() => {
    return [
      {
        column: 'search',
        value: this.filter(),
      }
    ];
  });

  generatePasswordResetLink(user: UserResponseDto): void {
    this.store.generatePasswordResetLink(user.userName, response => {
      const fullUrl = `${window.location.origin}${response.resetLink}`;

      // Copy to clipboard
      navigator.clipboard.writeText(fullUrl).then(() => {
        this.snackBar.open(`Password reset link copied to clipboard! Link expires at ${new Date(response.expiresAt).toLocaleString()}`, 'Close', {
          duration: 10000
        });
      }).catch(() => {
        // Fallback: show the link
        this.snackBar.open(`Password reset link: ${fullUrl} (Expires: ${new Date(response.expiresAt).toLocaleString()})`, 'Close', {
          duration: 15000
        });
      });
    });
  }

  columns: Column<UserResponseDto>[] = [
    {
      columnDef: 'userID',
      header: 'User ID',
      cell: (element: UserResponseDto) => `${element.userID}`,
    },
    {
      columnDef: 'userName',
      header: 'Username',
      cell: (element: UserResponseDto) => `${element.userName}`,
      // Links to the user's profile
      template: 2,
    },
    {
      columnDef: 'lockoutEnd',
      header: 'Lockout ends',
      cell: (element: UserResponseDto) => `${element.lockoutEnd}`, // pipe date short
      template: 1,
    },
    {
      columnDef: 'lockoutEnabled',
      header: 'Lockout enabled',
      cell: (element: UserResponseDto) => element.lockoutEnabled ? 'Yes' : 'No',
    },
    {
      columnDef: 'accessFailedCount',
      header: 'Failed sign-ins',
      cell: (element: UserResponseDto) => `${element.accessFailedCount}`,
    },
    {
      columnDef: 'region',
      header: 'Region',
      cell: (element: UserResponseDto) => element.region ?? '',
    },
    {
      columnDef: 'displayName',
      header: 'Display name',
      cell: (element: UserResponseDto) => element.displayName ?? '',
    },
    {
      columnDef: 'gamertag',
      header: 'Gamertag',
      cell: (element: UserResponseDto) => element.gamertag ?? '',
    },
    {
      columnDef: 'discord',
      header: 'Discord',
      cell: (element: UserResponseDto) => element.discord ?? '',
    },
    {
      columnDef: 'externalAuthCount',
      header: 'Linked logins',
      cell: (element: UserResponseDto) => `${element.externalAuthCount}`,
    },
    {
      columnDef: 'actions',
      header: 'Actions',
      cell: (element: UserResponseDto) => `IGNOREME`,
      template: 0,
      isSortable: false,
    },
  ];
}
