import { ChangeDetectionStrategy, Component, ContentChild, ContentChildren, input, QueryList, signal, TemplateRef } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { getPaginationResource } from '../getPaginationResource';
import { animate, style, transition, trigger } from '@angular/animations';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-table',
  imports: [
    MatTableModule,
    MatPaginator,
    MatSortModule,
    CommonModule,
    RouterModule,
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
  templateUrl: './table.component.html',
  styleUrl: './table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableComponent<T> {
  @ContentChild(TemplateRef) rowTemplate!: TemplateRef<any>;
  @ContentChildren(TemplateRef) rowTemplates!: QueryList<TemplateRef<any>>;

  filters = input<Filter[]>();
  
  displayedColumns = input.required<string[]>();
  url = input.required<string>();
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

  paginationResource = getPaginationResource<T>(this.url, {
      pageSize: () => this.pageSize(),
      pageNumber: () => this.pageNumber(),
      sortDirection: () => this.sort()?.direction,
      sortColumn: () => this.sort()?.active,
    }, () => this.filters());
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