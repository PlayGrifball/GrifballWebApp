import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TableComponent, Column, Filter } from './table.component';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Sort } from '@angular/material/sort';
import { PageEvent } from '@angular/material/paginator';

describe('TableComponent', () => {
  let component: TableComponent<TestData>;
  let fixture: ComponentFixture<TableComponent<TestData>>;

  interface TestData {
    id: number;
    name: string;
  }

  const testColumns: Column<TestData>[] = [
    {
      columnDef: 'id',
      header: 'ID',
      cell: (element: TestData) => `${element.id}`,
      isSortable: true
    },
    {
      columnDef: 'name',
      header: 'Name',
      cell: (element: TestData) => element.name,
      isSortable: true
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TableComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TableComponent<TestData>);
    component = fixture.componentInstance;
    
    // Set required inputs
    fixture.componentRef.setInput('displayedColumns', ['id', 'name']);
    fixture.componentRef.setInput('url', '/api/test');
    fixture.componentRef.setInput('columns', testColumns);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have default pageSize of 10', () => {
    expect(component.pageSize()).toBe(10);
  });

  it('should have default pageNumber of 1', () => {
    expect(component.pageNumber()).toBe(1);
  });

  it('should have undefined sort initially', () => {
    expect(component.sort()).toBeUndefined();
  });

  it('should have isSortableDefault true by default', () => {
    expect(component.isSortableDefault()).toBe(true);
  });

  it('should handle page change event', () => {
    const pageEvent: PageEvent = {
      pageIndex: 2,
      pageSize: 20,
      length: 100
    };

    component.onPageChange(pageEvent);

    expect(component.pageNumber()).toBe(3); // pageIndex + 1
    expect(component.pageSize()).toBe(20);
  });

  it('should handle sort change event', () => {
    const sortEvent: Sort = {
      active: 'name',
      direction: 'asc'
    };

    component.onSortChange(sortEvent);

    expect(component.sort()).toEqual(sortEvent);
  });

  it('should handle descending sort', () => {
    const sortEvent: Sort = {
      active: 'id',
      direction: 'desc'
    };

    component.onSortChange(sortEvent);

    expect(component.sort()?.direction).toBe('desc');
    expect(component.sort()?.active).toBe('id');
  });

  it('should accept filters input', () => {
    const filters: Filter[] = [
      { column: 'status', value: 'active' },
      { column: 'type', value: 'user' }
    ];

    fixture.componentRef.setInput('filters', filters);
    fixture.detectChanges();

    expect(component.filters()).toEqual(filters);
  });

  it('should accept isSortableDefault input', () => {
    fixture.componentRef.setInput('isSortableDefault', false);
    fixture.detectChanges();

    expect(component.isSortableDefault()).toBe(false);
  });

  it('should create pagination resource', () => {
    expect(component.paginationResource).toBeDefined();
    expect(component.x).toBeDefined();
    expect(component.current).toBeDefined();
  });

  it('should handle multiple page changes', () => {
    component.onPageChange({ pageIndex: 0, pageSize: 10, length: 100 });
    expect(component.pageNumber()).toBe(1);

    component.onPageChange({ pageIndex: 1, pageSize: 10, length: 100 });
    expect(component.pageNumber()).toBe(2);

    component.onPageChange({ pageIndex: 5, pageSize: 25, length: 100 });
    expect(component.pageNumber()).toBe(6);
    expect(component.pageSize()).toBe(25);
  });

  it('should handle empty sort event', () => {
    const sortEvent: Sort = {
      active: '',
      direction: ''
    };

    component.onSortChange(sortEvent);

    expect(component.sort()).toEqual(sortEvent);
  });
});
