import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { UserResponseDto } from './userResponseDto';
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
    ],
    templateUrl: './userManagement.component.html',
    styleUrl: './userManagement.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserManagementComponent {
  public displayedColumns: string[] = ['userID', 'userName', 'lockoutEnd', 'lockoutEnabled', 'accessFailedCount', 'region', 'displayName', 'gamertag', 'discord', 'externalAuthCount', 'actions'];
  url = '/api/UserManagement/GetUsers';

  filter = signal<string>('');
  filters = computed<Filter[]>(() => {
    return [
      {
        column: 'search',
        value: this.filter(),
      }
    ];
  });

  constructor(
    private accountService: AccountService,
    private snackBar: MatSnackBar
  ) {}

  generatePasswordResetLink(user: UserResponseDto): void {
    if (user.externalAuthCount > 0 && !this.hasInternalLogin(user)) {
      this.snackBar.open('This user only uses external authentication (Discord). Password reset is not applicable.', 'Close', {
        duration: 5000
      });
      return;
    }

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

  private hasInternalLogin(user: UserResponseDto): boolean {
    // Assume if external auth count is less than 1, they have internal login
    // Or if they have external auth but this is a special case where they also have a password
    // This logic might need to be refined based on your actual user data structure
    return user.externalAuthCount === 0;
  }

  columns: Column<UserResponseDto>[] = [
    {
      columnDef: 'userID',
      header: 'User ID',
      cell: (element: UserResponseDto) => `${element.userID}`,
    },
    {
      columnDef: 'userName',
      header: 'userName',
      cell: (element: UserResponseDto) => `${element.userName}`,
    },
    {
      columnDef: 'lockoutEnd',
      header: 'lockoutEnd',
      cell: (element: UserResponseDto) => `${element.lockoutEnd}`, // pipe date short
      template: 1,
    },
    {
      columnDef: 'lockoutEnabled',
      header: 'lockoutEnabled',
      cell: (element: UserResponseDto) => `${element.lockoutEnabled}`,
    },
    {
      columnDef: 'accessFailedCount',
      header: 'accessFailedCount',
      cell: (element: UserResponseDto) => `${element.accessFailedCount}`,
    },
    {
      columnDef: 'region',
      header: 'region',
      cell: (element: UserResponseDto) => `${element.region}`,
    },
    {
      columnDef: 'displayName',
      header: 'displayName',
      cell: (element: UserResponseDto) => `${element.displayName}`,
    },
    {
      columnDef: 'gamertag',
      header: 'gamertag',
      cell: (element: UserResponseDto) => `${element.gamertag}`,
    },
    {
      columnDef: 'discord',
      header: 'Discord',
      cell: (element: UserResponseDto) => `${element.discord}`,
    },
    {
      columnDef: 'externalAuthCount',
      header: 'External Auth Count',
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
