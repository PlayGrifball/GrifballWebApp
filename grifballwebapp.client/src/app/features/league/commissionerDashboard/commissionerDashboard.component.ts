import { Component, OnInit, ChangeDetectionStrategy, inject, signal, DestroyRef } from '@angular/core';
import { TitleCasePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ProcessRescheduleDialogComponent } from './processRescheduleDialog/processRescheduleDialog.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommissionerDashboardService } from '@api/services/commissioner-dashboard.service';
import { RescheduleService } from '@api/services/reschedule.service';
import type { CommissionerDashboardDto, RescheduleDto } from '@api/models';

export type { CommissionerDashboardDto, RescheduleDto };

@Component({
  selector: 'app-commissioner-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    MatTabsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatBadgeModule,
    MatDialogModule,
    MatTooltipModule,
    TitleCasePipe,
  ],
  templateUrl: './commissionerDashboard.component.html',
  styleUrls: ['./commissionerDashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CommissionerDashboardComponent implements OnInit {
  private commissionerDashboard = inject(CommissionerDashboardService);
  private rescheduleService = inject(RescheduleService);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  readonly dashboardData = signal<CommissionerDashboardDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  rescheduleDisplayedColumns: string[] = ['match', 'originalTime', 'newTime', 'requestedBy', 'reason', 'requestedAt', 'process', 'thread'];
  overdueDisplayedColumns: string[] = ['match', 'scheduledTime', 'hoursOverdue', 'severity'];

  ngOnInit(): void {
    this.loadDashboardData();
  }

  loadDashboardData(): void {
    this.loading.set(true);
    this.error.set(null);
    this.commissionerDashboard.getDashboardData()
      .subscribe({
        next: (data) => {
          this.dashboardData.set(data);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set('Failed to load dashboard data');
          console.error('Dashboard load error:', err);
          this.loading.set(false);
        }
      });
  }

  formatDateTime(dateStr?: string | null): string {
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

    dialogRef.afterClosed()
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(result => {
      if (result) {
        this.loadDashboardData(); // Refresh data after processing
      }
    });
  }

  createDiscordThread(reschedule: RescheduleDto): void {
    this.rescheduleService.createDiscordThread({ rescheduleID: reschedule.matchRescheduleID })
    .subscribe({
      next: (response: any) => {
        this.loadDashboardData(); // Refresh to show the thread ID
      },
      error: (error) => {
        this.loadDashboardData(); // Refresh to show the thread ID
        console.error('Failed to create Discord thread:', error);
      }
    });
  }
}