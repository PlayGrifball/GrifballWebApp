import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { ApiClientService } from '../api/apiClient.service';
import { ProcessRescheduleDialogComponent } from './process-reschedule-dialog/process-reschedule-dialog.component';

export interface CommissionerDashboardDto {
  pendingReschedules: RescheduleDto[];
  overdueMatches: OverdueMatchDto[];
  summary: DashboardSummaryDto;
}

export interface RescheduleDto {
  matchRescheduleID: number;
  seasonMatchID: number;
  homeCaptain: string;
  awayCaptain: string;
  originalScheduledTime?: string;
  newScheduledTime?: string;
  reason: string;
  requestedByGamertag: string;
  requestedAt: string;
  status: number;
  discordThreadID?: number;
}

export interface OverdueMatchDto {
  seasonMatchID: number;
  homeCaptain: string;
  awayCaptain: string;
  scheduledTime: string;
  hoursOverdue: number;
}

export interface DashboardSummaryDto {
  pendingRescheduleCount: number;
  overdueMatchCount: number;
  criticalOverdueCount: number;
}

@Component({
  selector: 'app-commissioner-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTabsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatBadgeModule,
    MatChipsModule,
    MatDialogModule
  ],
  templateUrl: './commissioner-dashboard.component.html',
  styleUrls: ['./commissioner-dashboard.component.scss']
})
export class CommissionerDashboardComponent implements OnInit {
  dashboardData: CommissionerDashboardDto | null = null;
  loading = true;
  error: string | null = null;

  rescheduleDisplayedColumns: string[] = ['match', 'originalTime', 'newTime', 'requestedBy', 'reason', 'requestedAt', 'actions'];
  overdueDisplayedColumns: string[] = ['match', 'scheduledTime', 'hoursOverdue', 'severity'];

  constructor(
    private apiClient: ApiClientService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadDashboardData();
  }

  async loadDashboardData(): Promise<void> {
    try {
      this.loading = true;
      this.error = null;
      this.dashboardData = await this.apiClient.get<CommissionerDashboardDto>('CommissionerDashboard/GetDashboardData');
    } catch (error) {
      this.error = 'Failed to load dashboard data';
      console.error('Dashboard load error:', error);
    } finally {
      this.loading = false;
    }
  }

  formatDateTime(dateStr?: string): string {
    if (!dateStr) return 'Not scheduled';
    return new Date(dateStr).toLocaleString();
  }

  getOverdueSeverity(hoursOverdue: number): string {
    if (hoursOverdue > 72) return 'critical';
    if (hoursOverdue > 48) return 'warning';
    return 'normal';
  }

  getOverdueSeverityColor(hoursOverdue: number): string {
    if (hoursOverdue > 72) return 'warn';
    if (hoursOverdue > 48) return 'accent';
    return 'primary';
  }

  openProcessRescheduleDialog(reschedule: RescheduleDto): void {
    const dialogRef = this.dialog.open(ProcessRescheduleDialogComponent, {
      width: '600px',
      data: reschedule
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadDashboardData(); // Refresh data after processing
      }
    });
  }

  async createDiscordThread(reschedule: RescheduleDto): Promise<void> {
    try {
      // You'll need to configure the channel ID in your app config
      const channelId = '1234567890'; // Replace with actual channel ID
      await this.apiClient.post(`Reschedule/CreateDiscordThread/${reschedule.matchRescheduleID}`, { channelId });
      this.loadDashboardData(); // Refresh to show the thread ID
    } catch (error) {
      console.error('Failed to create Discord thread:', error);
    }
  }
}