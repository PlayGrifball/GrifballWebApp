import { animate, style, transition, trigger } from '@angular/animations';
import { DatePipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit, signal, viewChild, WritableSignal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort, MatSortModule, Sort, SortDirection } from '@angular/material/sort';
import { MatHeaderRow, MatRow, MatTable, MatTableDataSource, MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { combineLatest, Observable, of, switchMap, tap } from 'rxjs';

@Component({
  selector: 'app-home',
  standalone: true,
  templateUrl: 'home.component.html',
  imports: [
    DatePipe,
    RouterModule,
    MatPaginator,
    MatSortModule,
    MatTable,
    MatHeaderRow,
    MatRow,
    MatTableModule,
  ],
  animations: [
    trigger('rowsAnimation', [
      transition(':enter', [
          style({ opacity: .1 }),
          animate('350ms', style({ opacity: 1 }))
      ]),
      transition(':leave', [
          style({ display: 'none' })
      ])
  ])
  ],
})
export class HomeComponent implements OnInit {

  displayedColumns: string[] = ['name', 'start', 'end'];
  currentAndFutureEvents: WritableSignal<Event[]> = signal([]);
  total: WritableSignal<number> = signal(0);
  pastSeasons: MatTableDataSource<Event> = new MatTableDataSource<Event>([]);
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

  constructor(private http: HttpClient) {
  }

  ngOnInit(): void {
    this.getCurrentAndFutureEvents().subscribe({
      next: (result) => this.currentAndFutureEvents.set(result),
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

  getCurrentAndFutureEvents(): Observable<Event[]> {
    return this.http.get<Event[]>('/api/Home/CurrentAndFutureEvents/');
  }

  getPastSeasons(pageSize: number, pageNumber: number, sortDirection: SortDirection | undefined, sortColumn: string | undefined): Observable<PaginationResult<Event>> {
    let params = new HttpParams().set('pageSize', pageSize).set('pageNumber', pageNumber);
    if (sortColumn && sortDirection) {
      params = params.set('sortDirection', sortDirection);
      params = params.set('sortColumn', sortColumn);
    }

    return this.http.get<PaginationResult<Event>>('/api/Home/PastSeasons', { params })
    .pipe(
      tap((result) => {
      this.total.set(result.totalCount);
      this.pastSeasons.data = result.results;
    }));
  }

}

export interface Event
{
  seasonID: number,
  name: string,
  start: string,
  end: string,
  eventType: 'Signup' | 'Draft' | 'Season'
}

export interface PaginationResult<T>
{
  totalCount: number,
  results: T[],
}