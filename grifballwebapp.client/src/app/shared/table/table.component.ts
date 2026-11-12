import { ChangeDetectionStrategy, Component, input, signal, TemplateRef, contentChildren } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { getPaginationResource, PaginationParams } from '../getPaginationResource';
import { PaginationResult } from '../paginationResult';
import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { rowEnterAnimation } from '../rowEnterAnimation';

@Component({
  selector: 'app-table',
  imports: [
    MatTableModule,
    MatPaginator,
    MatSortModule,
    CommonModule,
    RouterModule,
    MatButtonModule,
  ],
  templateUrl: './table.component.html',
  styleUrl: './table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableComponent<T> {
  /** The rows' fade-in, once hydration is past (rowEnterAnimation) */
  readonly rowEnter = rowEnterAnimation();
  readonly rowTemplates = contentChildren(TemplateRef);

  filters = input<Filter[]>();
  /** Optional: turns each row from its JSON form (e.g. dates as strings) into a T. */
  parseRow = input<(row: any) => T>();
  
  displayedColumns = input.required<string[]>();
  /** This page's generated-service call for one page of rows (raw, pre-parseRow: see getPaginationResource). */
  request = input.required<(params: PaginationParams) => Observable<PaginationResult<any>>>();
  columns = input.required<Column<T>[]>();
  pageSize = signal(10);
  pageNumber = signal(1);
  sort = signal<Sort | undefined>(undefined);

  // We default to true to allow sorting, but the parent component can override this in case there are not many sortable columns
  isSortableDefault = input<boolean>(true);

  onPageChange(event: PageEvent) {
    this.pageNumber.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
  }

  onSortChange(event: Sort) {
    this.sort.set(event);
  }

  paginationResource = getPaginationResource<T>(params => this.request()(params), {
      pageSize: () => this.pageSize(),
      pageNumber: () => this.pageNumber(),
      sortDirection: () => this.sort()?.direction,
      sortColumn: () => this.sort()?.active,
    }, () => this.filters(), this.parseRow);
  x = this.paginationResource.resource;
  current = this.paginationResource.current;
}

export interface Column<T> {
  columnDef: string;
  header: string;
  cell: (element: T) => string;
  template?: number; // Optional, if you want to render custom template. cell will be ignored if template is provided.
  isSortable?: boolean;
  //isFilterable?: boolean;
  //filterValue?: (element: T) => string | number | boolean;
};

export interface Filter {
  column: string;
  value: string | number | boolean;
}