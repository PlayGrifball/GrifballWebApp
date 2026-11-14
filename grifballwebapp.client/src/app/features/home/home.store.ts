import { inject, Injectable, signal } from '@angular/core';
import type { SeasonListItemDto } from '@api/models';
import { HomeService } from '@api/services/home.service';
import { SeasonService } from '@api/services/season.service';
import { apiSortDirection } from '@shared/apiSort';
import { PaginationResult } from '@shared/paginationResult';
import { SeasonEvent } from '@shared/seasonEvents';
import { catchError, EMPTY, Observable, tap } from 'rxjs';

/** The home page's data: the current and coming season events, and the past seasons a page at a time. One per page (the component provides it). */
@Injectable()
export class HomeStore {
  private readonly home = inject(HomeService);
  private readonly seasonService = inject(SeasonService);

  private readonly _events = signal<SeasonEvent[]>([]);
  readonly events = this._events.asReadonly();
  /** The events have loaded (or failed to). */
  private readonly _eventsLoaded = signal(false);
  readonly eventsLoaded = this._eventsLoaded.asReadonly();

  /** The past seasons' page shown, and how many there are in all. */
  private readonly _pastSeasons = signal<SeasonListItemDto[]>([]);
  readonly pastSeasons = this._pastSeasons.asReadonly();
  private readonly _total = signal(0);
  readonly total = this._total.asReadonly();

  loadEvents(): void {
    this.home.currentAndFutureEvents().subscribe({
      next: events => {
        this._events.set(events);
        this._eventsLoaded.set(true);
      },
      error: () => this._eventsLoaded.set(true),
    });
  }

  /**
   * A page of the seasons that have ended (index.html fetches the first page early, with these parameters in this
   * order), shown once it arrives. An observable, so the page's paginator stream can drop a page it moved past; it never
   * fails (that would end the stream, and the table would page no more): a page that can't be read leaves the last shown.
   */
  loadPastSeasons(pageSize: number, pageNumber: number, sortDirection: string | undefined, sortColumn: string | undefined): Observable<PaginationResult<SeasonListItemDto>> {
    const sorted = !!(sortColumn && sortDirection);
    return this.seasonService.getSeasonList({
      ended: true,
      pageNumber,
      pageSize,
      sortDirection: sorted ? apiSortDirection(sortDirection) : undefined,
      sortColumn: sorted ? sortColumn : undefined,
    }).pipe(
      tap(result => {
        this._total.set(result.totalCount);
        this._pastSeasons.set(result.results);
      }),
      catchError(() => EMPTY),
    );
  }
}
