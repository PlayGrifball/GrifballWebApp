import { Injectable, inject } from '@angular/core';
import { finalize, Observable, share } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import type { SeasonSummaryDto } from '../api/dtos/seasonSummaryDto';

/**
 * Season summaries. Nothing is kept between pages, so a page always shows the season as it is now (after a result is
 * reported or the season is edited); pages that ask for the same season while it is on its way share the one request.
 */
@Injectable({ providedIn: 'root' })
export class SeasonSummaryService {
  private api = inject(ApiClientService);
  private readonly inFlight = new Map<number, Observable<SeasonSummaryDto>>();

  get(seasonID: number): Observable<SeasonSummaryDto> {
    let summary = this.inFlight.get(seasonID);
    if (!summary) {
      summary = this.api.getSeasonSummary(seasonID).pipe(
        // Done (or failed, or nobody is listening any more): the next page asks again
        finalize(() => this.inFlight.delete(seasonID)),
        // Shared while on its way only: nothing is replayed once it is done
        share());
      this.inFlight.set(seasonID, summary);
    }
    return summary;
  }
}

/**
 * A section of the season has nothing, and never will: the season was backfilled, or is over. It says so in place of
 * an empty table.
 */
export const missing = (summary: SeasonSummaryDto | null | undefined, count: (s: SeasonSummaryDto) => number) =>
  !!summary && count(summary) === 0 && (summary.isHistorical || summary.hasEnded);
