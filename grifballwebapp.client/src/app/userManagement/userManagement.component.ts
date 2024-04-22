import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { UserResponseDto } from './userResponseDto';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    RouterModule,
    MatButtonModule
  ],
  templateUrl: './userManagement.component.html',
  styleUrl: './userManagement.component.scss'
})
export class UserManagementComponent implements OnInit {
  public users: UserResponseDto[] = [];
  public displayedColumns: string[] = ['userID', 'userName', 'lockoutEnd', 'lockoutEnabled', 'accessFailedCount', 'region', 'displayName', 'gamertag', 'edit'];

  constructor(private http: HttpClient) {
  }

  ngOnInit(): void {
    this.getUsers();
  }

  getUsers(): void {
    this.http.get<UserResponseDto[]>('/api/UserManagement/GetUsers').subscribe({
      next: (result) => this.users = result,
      error: (error) => console.error(error),
    });
  }
}
