import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { SeasonService } from '@api/services/season.service';
import { SignupsService } from '@api/services/signups.service';
import { resourceView } from '@shared/resourceView';
import { StoreInput } from '@shared/storeInput';
import { tap } from 'rxjs';
import { SeasonSummaryService } from '../season/seasonSummary';
import { AppLog } from '@core/telemetry/appLog';
import { liveReload } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';

/**
 * A season's signups and summary: the route's season, or the current one when it names none (0). A new season drops
 * what was shown, and cancels any request still on its way for the last one. One per page (the component provides it).
 */
@Injectable()
export class SignupsStore {
  private readonly log = inject(AppLog);
  private readonly seasons = inject(SeasonService);
  private readonly signups = inject(SignupsService);
  private readonly summaries = inject(SeasonSummaryService);

  /** The route's season (0: the current season): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** The current season's ID, asked for only while the route names none. */
  private readonly current = resourceView(rxResource({
    params: () => this.seasonID.value() === 0 ? true : undefined,
    stream: () => this.seasons.getCurrentSeasonId(),
  }), null);
  /** The season shown: the route's, or the current season's ID once known (0 until then). */
  readonly shownSeasonID = computed(() => this.seasonID.value() || this.current.value() || 0);

  private readonly list = resourceView(rxResource({
    params: () => this.shownSeasonID() || undefined,
    stream: ({ params: seasonID }) => this.signups.getSignups({ seasonID })
      .pipe(tap({ error: e => this.log.error('Failed to load the signups', e) })),
  }), []);
  readonly signUps = this.list.value;
  /** The signups are on their way (the current season's ID first, when the route names none): a skeleton. */
  readonly firstLoad = computed(() => this.current.firstLoad() || this.list.firstLoad());
  /** The signups could not be read: the page says so, never "0 signed up". */
  readonly failed = this.list.failed;

  /** The season's summary (a backfilled season says it has no signups); none while it loads, or if it can't be read. */
  private readonly loadedSummary = resourceView(rxResource({
    params: () => this.shownSeasonID() || undefined,
    stream: ({ params: seasonID }) => this.summaries.get(seasonID),
  }), null);
  readonly summary = this.loadedSummary.value;

  constructor() {
    // A signup made, changed or withdrawn: the list again, in place
    liveReload(() => this.shownSeasonID() ? [LiveTopics.signups(this.shownSeasonID())] : [], () => this.list.reload());
  }
}
