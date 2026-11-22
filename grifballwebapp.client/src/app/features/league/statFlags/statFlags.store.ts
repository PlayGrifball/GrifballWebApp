import { inject, Injectable, signal } from '@angular/core';
import type { StatFlagDto } from '@api/models';
import { StaffStatFlagsService } from '@api/services/staff-stat-flags.service';
import { AppLog } from '@core/telemetry/appLog';

/** The staff flags page's open flags (#548), and dismissing one. One per page (the component provides it). */
@Injectable()
export class StatFlagsStore {
  private readonly log = inject(AppLog);
  private readonly statFlags = inject(StaffStatFlagsService);

  private readonly _flags = signal<StatFlagDto[] | null>(null);
  readonly flags = this._flags.asReadonly();
  private readonly _loading = signal(true);
  readonly loading = this._loading.asReadonly();
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  /** Loads the open flags (again): loading only while nothing is shown, so a reload keeps the list shown. */
  load(): void {
    if (!this._flags())
      this._loading.set(true);
    this._error.set(null);
    this.statFlags.getOpenStatFlags().subscribe({
      next: flags => {
        this._flags.set(flags);
        this._loading.set(false);
      },
      error: err => {
        this._error.set('Failed to load the open stat flags');
        this.log.error('Failed to load the open stat flags', err);
        this._loading.set(false);
      },
    });
  }

  /** Dismisses a flag, then drops it from the list shown (a reload would find it gone too).*/
  dismiss(statFlagID: number): void {
    this.statFlags.dismissStatFlag({ statFlagID }).subscribe({
      next: () => this._flags.update(flags => flags?.filter(f => f.statFlagID !== statFlagID) ?? flags),
      error: err => this.log.error('Failed to dismiss the stat flag', err),
    });
  }
}
