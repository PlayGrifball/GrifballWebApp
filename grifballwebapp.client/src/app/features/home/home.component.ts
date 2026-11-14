import { DatePipe } from '@angular/common';
import { Component, computed, OnInit, signal, viewChild, WritableSignal, ChangeDetectionStrategy, inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort, MatSortModule, Sort } from '@angular/material/sort';
import { MatHeaderRow, MatRow, MatTable, MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { combineLatest, of, switchMap, tap } from 'rxjs';
import { eventTitles, isUnderWay, SeasonEvent } from '@shared/seasonEvents';
import { pageNow } from '@shared/pageNow';
import { LateLeagueQueueComponent } from '../lateLeague';
import { rowEnterAnimation } from '@shared/rowEnterAnimation';
import { HomeStore } from './home.store';

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
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [HomeStore],
})
export class HomeComponent implements OnInit {
  /** The rows' fade-in, once hydration is past (rowEnterAnimation) */
  readonly rowEnter = rowEnterAnimation();
  private readonly store = inject(HomeStore);

  private datePipe = new DatePipe('en-US');

  displayedColumns: string[] = ['seasonName', 'champion', 'start', 'end'];
  readonly eventTitles = eventTitles;
  readonly eventLink = eventLink;
  /** When the page was opened (the server's time while the browser takes over its page, see
   * pageNow): decides which events are under way, and so whether Sign up is offered. */
  readonly now = pageNow();
  readonly currentAndFutureEvents = this.store.events;
  /** The events have loaded (or failed to): the page is shown then, in one go. */
  readonly eventsLoaded = this.store.eventsLoaded;
  /** The event the hero panel is about, with its status line and sentence; null without events. */
  readonly hero = computed(() => {
    const event = pickFeatured(this.currentAndFutureEvents(), this.now());
    return event && {
      event,
      kicker: heroKicker(event, this.now()),
      lede: heroLede(event, this.currentAndFutureEvents(), iso => this.datePipe.transform(iso, 'MMM d, h:mm a') ?? ''),
    };
  });
  readonly total = this.store.total;
  readonly pastSeasons = this.store.pastSeasons;
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
    this.store.loadEvents();

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
        switchMap((x) => this.store.loadPastSeasons(x.pageSize, x.pageNumber, x.sort?.direction, x.sort?.active))
      )
      .subscribe()
  }

  isActive(event: Event): boolean {
    return isUnderWay(event, this.now());
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
