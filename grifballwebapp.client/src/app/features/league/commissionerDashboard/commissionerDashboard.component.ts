import { Component, OnInit, ChangeDetectionStrategy, inject, DestroyRef } from '@angular/core';
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
import type { CommissionerDashboardDto, RescheduleDto } from '@api/models';
import { CommissionerDashboardStore } from './commissionerDashboard.store';

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
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CommissionerDashboardStore],
})
export class CommissionerDashboardComponent implements OnInit {
  private readonly store = inject(CommissionerDashboardStore);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  readonly dashboardData = this.store.dashboardData;
  readonly loading = this.store.loading;
  readonly error = this.store.error;

  rescheduleDisplayedColumns: string[] = ['match', 'originalTime', 'newTime', 'requestedBy', 'reason', 'requestedAt', 'process', 'thread'];
  overdueDisplayedColumns: string[] = ['match', 'scheduledTime', 'hoursOverdue', 'severity'];

  ngOnInit(): void {
    this.loadDashboardData();
  }

  loadDashboardData(): void {
    this.store.load();
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
    this.store.createDiscordThread(reschedule.matchRescheduleID);
  }
}
