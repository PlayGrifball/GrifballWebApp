import { inject, Injectable, signal } from '@angular/core';
import type { DraftPoolPlayer, DraftSuggestions } from '@api/models';
import { TeamsService } from '@api/services/teams.service';
import { catchError, forkJoin, of } from 'rxjs';

/** The draft helper's data: the pool in detail and the picks suggested for the team on deck. One per helper (the component provides it). */
@Injectable()
export class DraftHelperStore {
  private readonly api = inject(TeamsService);

  private readonly _pool = signal<DraftPoolPlayer[]>([]);
  /** The players still to be drafted, with their careers, timeslots and region. */
  readonly pool = this._pool.asReadonly();
  private readonly _suggestions = signal<DraftSuggestions | null>(null);
  /** The picks suggested for the team on deck; null without one (no captains yet). */
  readonly suggestions = this._suggestions.asReadonly();
  /** The first answer is in: a skeleton until then. A reload (a pick made) keeps the last shown. */
  private readonly _loaded = signal(false);
  readonly loaded = this._loaded.asReadonly();
  private loads = 0;

  /** Loads the season's pool and suggestions; an earlier load answered late is dropped. */
  load(seasonID: number): void {
    const load = ++this.loads;
    forkJoin({
      pool: this.api.getDraftPoolDetails({ seasonID }).pipe(catchError(() => of([] as DraftPoolPlayer[]))),
      // No team to suggest picks for is a 404: no suggestions, the pool all the same
      suggestions: this.api.getDraftSuggestions({ seasonID }).pipe(catchError(() => of(null))),
    }).subscribe(({ pool, suggestions }) => {
      if (load !== this.loads)
        return;
      this._pool.set(pool);
      this._suggestions.set(suggestions);
      this._loaded.set(true);
    });
  }
}
