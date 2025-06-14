import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterModule } from '@angular/router';
import { UserResponseDto } from './userResponseDto';
import { Column, TableComponent } from '../shared/table/table.component';

@Component({
    selector: 'app-user-management',
    imports: [
        CommonModule,
        RouterModule,
        MatButtonModule,
        TableComponent,
    ],
    templateUrl: './userManagement.component.html',
    styleUrl: './userManagement.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserManagementComponent {
  public displayedColumns: string[] = ['userID', 'userName', 'lockoutEnd', 'lockoutEnabled', 'accessFailedCount', 'region', 'displayName', 'gamertag', 'edit'];
  url = '/api/UserManagement/GetUsers';

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
      columnDef: 'edit',
      header: 'edit',
      cell: (element: UserResponseDto) => `IGNOREME`,
      template: 0,
    },
  ];
}
