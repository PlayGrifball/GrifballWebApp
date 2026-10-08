import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TableComponent, Column, Filter } from './table.component';
import { signal } from '@angular/core';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
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
        provideHttpClient(withXhr()),
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

describe('TableComponent API responses', () => {
  interface Row { id: number; name: string; }

  let fixture: ComponentFixture<TableComponent<Row>>;
  let component: TableComponent<Row>;
  let httpMock: HttpTestingController;

  const columns: Column<Row>[] = [
    { columnDef: 'id', header: 'ID', cell: r => `${r.id}` },
    { columnDef: 'name', header: 'Name', cell: r => r.name },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TableComponent, NoopAnimationsModule],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TableComponent<Row>);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('displayedColumns', ['id', 'name']);
    fixture.componentRef.setInput('url', '/api/test');
    fixture.componentRef.setInput('columns', columns);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const pending = () => httpMock.expectOne(r => r.url.startsWith('/api/test'));

  it('renders rows on success', async () => {
    pending().flush({ totalCount: 2, results: [{ id: 1, name: 'alpha' }, { id: 2, name: 'beta' }] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.current().totalCount).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('tr[mat-row]').length).toBe(2);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('does not throw on a server error and shows an error message', async () => {
    pending().flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect(component.x.status()).toBe('error');
    expect(() => component.current()).not.toThrow();
    expect(component.current()).toEqual({ results: [], totalCount: 0 });
    expect(() => fixture.detectChanges()).not.toThrow();

    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    expect(alert).not.toBeNull();
    expect(alert.textContent).toContain('Failed to load data');
    expect(fixture.nativeElement.querySelectorAll('tr[mat-row]').length).toBe(0);
  });

  it('does not throw on a 403 error', async () => {
    pending().flush(null, { status: 403, statusText: 'Forbidden' });
    await fixture.whenStable();
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(text()).toContain('Failed to load data');
  });

  it('recovers when retried after an error', async () => {
    pending().flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[role="alert"] button') as HTMLButtonElement).click();
    fixture.detectChanges();
    pending().flush({ totalCount: 1, results: [{ id: 1, name: 'alpha' }] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('tr[mat-row]').length).toBe(1);
  });

  it('shows an empty state when there are no results', async () => {
    pending().flush({ totalCount: 0, results: [] });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(text()).toContain('No results found');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });
});
