import { Component, ChangeDetectionStrategy, inject, input, signal, effect, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import type { StatFlagDto } from '@api/models';
import { StaffStatFlagsService } from '@api/services/staff-stat-flags.service';
import { AppLog } from '@core/telemetry/appLog';
import { liveReload } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';

/**
 * Open stat flags (#548) of a season match's games, for staff: a question, never a change. Renders nothing while
 * there are none (the common case), so it costs the page one request and no layout when there's nothing to say.
 */
@Component({
  selector: 'app-stat-flags-notice',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './statFlagsNotice.component.html',
  styleUrls: ['./statFlagsNotice.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatFlagsNoticeComponent {
  private readonly log = inject(AppLog);
  private readonly statFlags = inject(StaffStatFlagsService);

  readonly seasonMatchID = input.required<number>();

  private readonly _flags = signal<StatFlagDto[]>([]);
  readonly flags = this._flags.asReadonly();

  constructor() {
    effect(() => {
      // Read as a signal so a change of match (or a stat flag found or dismissed anywhere) reloads it
      this.seasonMatchID();
      untracked(() => this.load());
    });
    liveReload(() => [LiveTopics.staffStatFlags], () => this.load());
  }

  private load(): void {
    this.statFlags.getSeasonMatchStatFlags({ seasonMatchID: this.seasonMatchID() }).subscribe({
      next: flags => this._flags.set(flags),
      error: err => this.log.error('Failed to load this match\'s stat flags', err),
    });
  }

  dismiss(flag: StatFlagDto): void {
    this.statFlags.dismissStatFlag({ statFlagID: flag.statFlagID }).subscribe({
      next: () => this._flags.update(flags => flags.filter(f => f.statFlagID !== flag.statFlagID)),
      error: err => this.log.error('Failed to dismiss the stat flag', err),
    });
  }
}
