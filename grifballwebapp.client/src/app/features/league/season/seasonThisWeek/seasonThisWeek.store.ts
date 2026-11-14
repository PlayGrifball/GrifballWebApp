import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatchPlannerService } from '@api/services/match-planner.service';
import { StoreInput } from '@shared/storeInput';
import { map } from 'rxjs';
import { parseScheduledMatch, ScheduledMatchDto } from '../scheduleList/scheduledMatchDto';

/** A season's scheduled matches, for its "This week" cards. One per panel (the component provides it). */
@Injectable()
export class SeasonThisWeekStore {
  private readonly api = inject(MatchPlannerService);

  /** The season (0: none): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** Loaded again for each season: a new one cancels the request still on its way. */
  private readonly schedule = rxResource({
    params: () => this.seasonID.value() || undefined,
    stream: ({ params: seasonID }) => this.api.getScheduledMatches({ seasonID }).pipe(map(matches => matches.map(parseScheduledMatch))),
  });
  /** The season's scheduled matches; none for 0, or when they can't be read. */
  readonly matches = computed<ScheduledMatchDto[]>(() => this.schedule.hasValue() ? this.schedule.value() : []);
}
