import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { DraftPoolPlayer, DraftSuggestions } from '@api/models';
import { TeamsService } from '@api/services/teams.service';
import { resourceView } from '@shared/resourceView';
import { StoreInput } from '@shared/storeInput';
import { catchError, forkJoin, of, tap } from 'rxjs';

/** What the helper shows: the pool in detail, and the picks suggested for the team on deck (none without one). */
interface DraftHelp {
  pool: DraftPoolPlayer[];
  suggestions: DraftSuggestions | null;
}

/** The draft helper's data: the pool in detail and the picks suggested for the team on deck. One per helper (the component provides it). */
@Injectable()
export class DraftHelperStore {
  private readonly api = inject(TeamsService);

  /** The season: the component's input. */
  readonly seasonID = new StoreInput<number>();
  /** The draft's state as the page has it: the component's input. A change (a pick made) reads the helper again. */
  readonly draftKey = new StoreInput<string>();

  /** The last answer, and its season: what is shown while the next one (a pick made) loads. */
  private readonly last = signal<{ seasonID: number, help: DraftHelp } | null>(null);

  /** Read again for each season and draft state: a newer one cancels the request still on its way. */
  private readonly help = resourceView(rxResource({
    params: () => {
      const seasonID = this.seasonID.value();
      return seasonID === undefined ? undefined : { seasonID, draftKey: this.draftKey.value() };
    },
    stream: ({ params: { seasonID } }) => forkJoin({
      // Each part on its own: no pool is an empty one; no team to suggest picks for is a 404, the pool all the same
      pool: this.api.getDraftPoolDetails({ seasonID }).pipe(catchError(() => of([] as DraftPoolPlayer[]))),
      suggestions: this.api.getDraftSuggestions({ seasonID }).pipe(catchError(() => of(null))),
    }).pipe(tap(help => this.last.set({ seasonID, help }))),
  }), null);
  /** What is shown: the last answer, for the season shown (another season starts over). */
  private readonly shown = computed(() => {
    const last = this.last();
    return last && last.seasonID === this.seasonID.value() ? last.help : null;
  });

  /** The players still to be drafted, with their careers, timeslots and region. */
  readonly pool = computed(() => this.shown()?.pool ?? []);
  /** The picks suggested for the team on deck; null without one (no captains yet). */
  readonly suggestions = computed(() => this.shown()?.suggestions ?? null);
  /** Nothing to show yet, and the first answer on its way: a skeleton. */
  readonly firstLoad = computed(() => this.shown() === null && this.help.isLoading());
}
