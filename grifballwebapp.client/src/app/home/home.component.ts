import { DatePipe } from '@angular/common';
import { Component, computed, OnInit, signal, viewChild, WritableSignal, ChangeDetectionStrategy, inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort, MatSortModule, Sort, SortDirection } from '@angular/material/sort';
import { MatHeaderRow, MatRow, MatTable, MatTableDataSource, MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { combineLatest, Observable, of, switchMap, tap } from 'rxjs';
import { PaginationResult } from '../shared/paginationResult';
import { eventTitles, isUnderWay, SeasonEvent } from '../shared/seasonEvents';
import { pageNow } from '../shared/pageNow';
import type { SeasonListItemDto } from '@api/models';
import { apiSortDirection } from '../shared/apiSort';
import { LateLeagueQueueComponent } from '../lateLeague/lateLeagueQueue/lateLeagueQueue.component';
import { rowEnterAnimation } from '../shared/rowEnterAnimation';
import { HomeService } from '@api/services/home.service';
import { SeasonService } from '@api/services/season.service';

@Component({
    selector: 'app-home',
    templateUrl: 'home.component.html',
    styleUrl: 'home.component.scss',
    imports: [
        DatePipe,
        RouterModule,
        MatPaginator,
        MatSortModule,
        MatTable,
        MatHeaderRow,
        MatRow,
        MatTableModule,
        LateLeagueQueueComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class HomeComponent implements OnInit {
  /** The rows' fade-in, once hydration is past (rowEnterAnimation) */
  readonly rowEnter = rowEnterAnimation();
  private home = inject(HomeService);
  private seasonService = inject(SeasonService);


  private datePipe = new DatePipe('en-US');

  displayedColumns: string[] = ['seasonName', 'champion', 'start', 'end'];
  readonly eventTitles = eventTitles;
  readonly eventLink = eventLink;
  /** When the page was opened (the server's time while the browser takes over its page, see
   * pageNow): decides which events are under way, and so whether Sign up is offered. */
  readonly now = pageNow();
  currentAndFutureEvents: WritableSignal<Event[]> = signal([]);
  /** The events have loaded (or failed to): the page is shown then, in one go. */
  readonly eventsLoaded = signal(false);
  /** The event the hero panel is about, with its status line and sentence; null without events. */
  readonly hero = computed(() => {
    const event = pickFeatured(this.currentAndFutureEvents(), this.now());
    return event && {
      event,
      kicker: heroKicker(event, this.now()),
      lede: heroLede(event, this.currentAndFutureEvents(), iso => this.datePipe.transform(iso, 'MMM d, h:mm a') ?? ''),
    };
  });
  total: WritableSignal<number> = signal(0);
  pastSeasons: MatTableDataSource<SeasonListItemDto> = new MatTableDataSource<SeasonListItemDto>([]);
  paginator = viewChild(MatPaginator);
  paginator$ = toObservable(this.paginator);
  pageSize: WritableSignal<number> = signal(10);
  pageNumber: WritableSignal<number> = signal(1);
  pageSize$ = toObservable(this.pageSize);
  pageNumber$ = toObservable(this.pageNumber);
  sort = viewChild(MatSort);
  sort$ = toObservable(this.sort);
  s: WritableSignal<Sort | undefined> = signal(undefined);
  s$ = toObservable(this.s);

  ngOnInit(): void {
    this.getCurrentAndFutureEvents().subscribe({
      next: (result) => {
        this.currentAndFutureEvents.set(result);
        this.eventsLoaded.set(true);
      },
      error: () => this.eventsLoaded.set(true),
    });
    
    this.paginator$
      .pipe(
        switchMap((x) => x?.page ?? of(undefined)),
        tap((x) => {
          this.pageSize.set(x?.pageSize ?? 1);
          this.pageNumber.set((x?.pageIndex ?? 0) + 1);
        }),
      )
      .subscribe();

    this.sort$
      .pipe(
        switchMap((x) => x?.sortChange ?? of(undefined)),
        tap((x) => {
          this.s.set(x);
        }),
      )
      .subscribe();

    combineLatest({pageSize: this.pageSize$,
                   pageNumber: this.pageNumber$,
                   sort: this.s$,
                  })
      .pipe(
        switchMap((x) => this.getPastSeasons(x.pageSize, x.pageNumber, x.sort?.direction, x.sort?.active))
      )
      .subscribe()
  }

  isActive(event: Event): boolean {
    return isUnderWay(event, this.now());
  }

  getCurrentAndFutureEvents(): Observable<Event[]> {
    // The generated Event is this shape already (historicalSourceUrl is optional here, and not in its schema);
    // cast since Observable<T> doesn't structurally widen T on its own.
    return this.home.currentAndFutureEvents() as unknown as Observable<Event[]>;
  }

  getPastSeasons(pageSize: number, pageNumber: number, sortDirection: SortDirection | undefined, sortColumn: string | undefined): Observable<PaginationResult<SeasonListItemDto>> {
    // The season list's seasons that have ended (index.html fetches the first page early, with these parameters in this order)
    const sorted = !!(sortColumn && sortDirection);
    return this.seasonService.getSeasonList({
      ended: true,
      pageNumber,
      pageSize,
      sortDirection: sorted ? apiSortDirection(sortDirection) : undefined,
      sortColumn: sorted ? sortColumn : undefined,
    })
    .pipe(
      tap((result) => {
      this.total.set(result.totalCount);
      this.pastSeasons.data = result.results;
    }));
  }

}

/** A current, future or past season event (see shared/seasonEvents.ts). */
export type Event = SeasonEvent;

/**
 * The event to feature on the home page: one under way if any (signups first, as they need
 * players to act, then the draft, then the season), otherwise the next one to start.
 */
export function pickFeatured(events: Event[], now: number): Event | null {
  const priority: Event['eventType'][] = ['Signup', 'Draft', 'Season'];
  const underWay = events.filter(e => isUnderWay(e, now))
    .sort((a, b) => priority.indexOf(a.eventType) - priority.indexOf(b.eventType));
  if (underWay.length > 0)
    return underWay[0];
  const upcoming = [...events].sort((a, b) => Date.parse(a.start) - Date.parse(b.start));
  return upcoming[0] ?? null;
}

/** The hero's status line for the featured event. */
export function heroKicker(event: Event, now: number): string {
  const active = isUnderWay(event, now);
  switch (event.eventType) {
    case 'Signup': return active ? 'Signups online' : 'Signups incoming';
    case 'Draft': return active ? 'Draft in progress' : 'Draft incoming';
    default: return active ? 'Season live' : 'Season incoming';
  }
}

/** The hero's sentence; for signups it also gives the draft date when that is known. */
export function heroLede(event: Event, events: Event[], format: (iso: string) => string): string {
  switch (event.eventType) {
    case 'Signup': {
      const draft = events.find(e => e.seasonID === event.seasonID && e.eventType === 'Draft');
      return `Signups close ${format(event.end)}.` + (draft ? ` Captains draft on ${format(draft.start)}.` : '') + ' Pick up a hammer.';
    }
    case 'Draft': return `The draft starts ${format(event.start)} and runs until all picks are made.`;
    default: return `The season runs from ${format(event.start)} to ${format(event.end)}.`;
  }
}

/** Where an event's card leads. */
export function eventLink(event: Event): (string | number)[] {
  switch (event.eventType) {
    case 'Signup': return ['/season', event.seasonID, 'signups'];
    case 'Draft': return ['/season', event.seasonID, 'draft'];
    default: return ['/season', event.seasonID];
  }
}
