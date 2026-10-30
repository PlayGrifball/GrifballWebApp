import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, signal, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { parseUser, UserResponseDto } from './userResponseDto';
import { ToDatePipe } from '../shared/toDate.pipe';
import { Column, Filter, TableComponent } from '../shared/table/table.component';
import { SearchBoxComponent } from '../shared/searchBox/searchBox.component';
import { AccountService } from '../account.service';
import { GeneratePasswordResetLinkRequestDto } from '../api/dtos/passwordResetDtos';

@Component({
    selector: 'app-user-management',
    imports: [
        CommonModule,
        RouterModule,
        MatButtonModule,
        MatIconModule,
        MatTooltipModule,
        TableComponent,
        SearchBoxComponent,
        ToDatePipe,
    ],
    templateUrl: './userManagement.component.html',
    styleUrl: './userManagement.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserManagementComponent {
  private accountService = inject(AccountService);
  private snackBar = inject(MatSnackBar);

  public displayedColumns: string[] = ['userID', 'userName', 'lockoutEnd', 'lockoutEnabled', 'accessFailedCount', 'region', 'displayName', 'gamertag', 'discord', 'externalAuthCount', 'actions'];
  url = '/api/UserManagement/GetUsers';
  // Rows of the user list are never null (PaginationResult<UserResponseDto> on the server).
  readonly parseUser = (row: unknown) => parseUser(row)!;

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
    const request: GeneratePasswordResetLinkRequestDto = {
      username: user.userName
    };

    this.accountService.generatePasswordResetLink(request).subscribe({
      next: (response) => {
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
      },
      error: (error) => {
        let errorMessage = 'Failed to generate password reset link';
        if (error.error && typeof error.error === 'string') {
          errorMessage = error.error;
        }
        this.snackBar.open(errorMessage, 'Close', {
          duration: 5000
        });
      }
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
