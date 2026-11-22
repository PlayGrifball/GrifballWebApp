import { inject, Injectable, signal } from '@angular/core';
import type { CommissionerDashboardDto } from '@api/models';
import { CommissionerDashboardService } from '@api/services/commissioner-dashboard.service';
import { RescheduleService } from '@api/services/reschedule.service';
import { AppLog } from '@core/telemetry/appLog';
import { liveReload } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';

/** The commissioner dashboard's data (reschedule requests, overdue matches), and making a request's Discord thread. One per page (the component provides it). */
@Injectable()
export class CommissionerDashboardStore {
  private readonly log = inject(AppLog);
  private readonly dashboard = inject(CommissionerDashboardService);
  private readonly reschedules = inject(RescheduleService);

  private readonly _dashboardData = signal<CommissionerDashboardDto | null>(null);
  readonly dashboardData = this._dashboardData.asReadonly();
  private readonly _loading = signal(true);
  readonly loading = this._loading.asReadonly();
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  constructor() {
    // A reschedule asked for or decided, a match played, a stat flag found or dismissed: the dashboard again, in place
    liveReload(() => [LiveTopics.staffReschedules, LiveTopics.seasons, LiveTopics.staffStatFlags], () => this.load());
  }

  /** Loads the dashboard (again): loading only while nothing is shown, so a reload keeps it shown. */
  load(): void {
    if (!this._dashboardData())
      this._loading.set(true);
    this._error.set(null);
    this.dashboard.getDashboardData().subscribe({
      next: data => {
        this._dashboardData.set(data);
        this._loading.set(false);
      },
      error: err => {
        this._error.set('Failed to load dashboard data');
        this.log.error('Failed to load the commissioner dashboard', err);
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
        this.log.error('Failed to create the Discord thread', error);
      },
    });
  }
}
