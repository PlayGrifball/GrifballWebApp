import { inject, Injectable, signal } from '@angular/core';
import type { CommissionerDashboardDto } from '@api/models';
import { CommissionerDashboardService } from '@api/services/commissioner-dashboard.service';
import { RescheduleService } from '@api/services/reschedule.service';

/** The commissioner dashboard's data (reschedule requests, overdue matches), and making a request's Discord thread. One per page (the component provides it). */
@Injectable()
export class CommissionerDashboardStore {
  private readonly dashboard = inject(CommissionerDashboardService);
  private readonly reschedules = inject(RescheduleService);

  private readonly _dashboardData = signal<CommissionerDashboardDto | null>(null);
  readonly dashboardData = this._dashboardData.asReadonly();
  private readonly _loading = signal(true);
  readonly loading = this._loading.asReadonly();
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  load(): void {
    this._loading.set(true);
    this._error.set(null);
    this.dashboard.getDashboardData().subscribe({
      next: data => {
        this._dashboardData.set(data);
        this._loading.set(false);
      },
      error: err => {
        this._error.set('Failed to load dashboard data');
        console.error('Dashboard load error:', err);
        this._loading.set(false);
      },
    });
  }

  /** Makes a reschedule request's Discord thread, then loads the dashboard again to show it (or where it stands). */
  createDiscordThread(rescheduleID: number): void {
    this.reschedules.createDiscordThread({ rescheduleID }).subscribe({
      next: () => this.load(),
      error: error => {
        this.load();
        console.error('Failed to create Discord thread:', error);
      },
    });
  }
}
