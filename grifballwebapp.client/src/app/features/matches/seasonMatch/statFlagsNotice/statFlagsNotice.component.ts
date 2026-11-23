import { Component, ChangeDetectionStrategy, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import type { StatFlagDto } from '@api/models';
import { StaffStatFlagsService } from '@api/services/staff-stat-flags.service';
import { AppLog } from '@core/telemetry/appLog';
import { liveReload } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';
import { resourceView } from '@shared/resourceView';
import { tap } from 'rxjs';

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

  /**
   * The match's open stat flags, reloaded for each match ID: a new one cancels the request still on its way
   * (rxResource's own params-keyed switchMap), so a slower response for an earlier match can never show on a later
   * one's - or be the one Dismiss acts against (third review: was a bare subscribe with nothing to cancel it).
   */
  private readonly resource = resourceView(rxResource({
    params: () => ({ seasonMatchID: this.seasonMatchID() }),
    stream: ({ params }) => this.statFlags.getSeasonMatchStatFlags(params)
      .pipe(tap({ error: e => this.log.error('Failed to load this match\'s stat flags', e) })),
  }), []);
  readonly flags = this.resource.value;

  constructor() {
    // A stat flag found or dismissed anywhere: the list again, in place
    liveReload(() => [LiveTopics.staffStatFlags], () => this.resource.reload());
  }

  dismiss(flag: StatFlagDto): void {
    this.statFlags.dismissStatFlag({ statFlagID: flag.statFlagID }).subscribe({
      next: () => this.resource.set(this.flags().filter(f => f.statFlagID !== flag.statFlagID)),
      error: err => this.log.error('Failed to dismiss the stat flag', err),
    });
  }
}
