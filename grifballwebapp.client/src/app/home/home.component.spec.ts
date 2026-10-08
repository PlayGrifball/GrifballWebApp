import { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { HomeComponent } from './home.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('HomeComponent', () => {
  let component: HomeComponent;
  let httpTestingController: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent, HttpClientTestingModule, NoopAnimationsModule]
    }).compileComponents();

    const fixture = TestBed.createComponent(HomeComponent);
    component = fixture.componentInstance;
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have correct displayed columns', () => {
    expect(component.displayedColumns).toEqual(['name', 'start', 'end']);
  });

  it('should initialize with empty current and future events', () => {
    expect(component.currentAndFutureEvents()).toEqual([]);
  });

  it('should initialize with total count of 0', () => {
    expect(component.total()).toBe(0);
  });

  it('should initialize with page size of 10', () => {
    expect(component.pageSize()).toBe(10);
  });

  it('should initialize with page number of 1', () => {
    expect(component.pageNumber()).toBe(1);
  });

  it('should fetch current and future events on init', () => {
    const mockEvents = [
      { seasonID: 1, name: 'Event 1', start: '2024-01-01', end: '2024-01-31', eventType: 'Season' as const },
      { seasonID: 2, name: 'Event 2', start: '2024-02-01', end: '2024-02-28', eventType: 'Draft' as const }
    ];

    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/Home/CurrentAndFutureEvents/');
    expect(req.request.method).toBe('GET');
    req.flush(mockEvents);

    expect(component.currentAndFutureEvents()).toEqual(mockEvents);
  });

  describe('getCurrentAndFutureEvents', () => {
    it('should make GET request to correct endpoint', (done) => {
      const mockEvents = [
        { seasonID: 1, name: 'Test Event', start: '2024-01-01', end: '2024-01-31', eventType: 'Signup' as const }
      ];

      component.getCurrentAndFutureEvents().subscribe({
        next: (events) => {
          expect(events).toEqual(mockEvents);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Home/CurrentAndFutureEvents/');
      expect(req.request.method).toBe('GET');
      req.flush(mockEvents);
    });
  });

  describe('getPastSeasons', () => {
    it('should make GET request with pagination parameters', (done) => {
      const mockResult = {
        results: [
          { seasonID: 3, name: 'Past Event', start: '2023-01-01', end: '2023-01-31', eventType: 'Season' as const }
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10
      };

      component.getPastSeasons(10, 1, undefined, undefined).subscribe({
        next: (result) => {
          expect(result).toEqual(mockResult);
          expect(component.total()).toBe(1);
          expect(component.pastSeasons.data).toEqual(mockResult.results);
          done();
        }
      });

      const req = httpTestingController.expectOne((request) => 
        request.url === '/api/Home/PastSeasons' &&
        request.params.get('pageSize') === '10' &&
        request.params.get('pageNumber') === '1'
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockResult);
    });

    it('should include sort parameters when provided', (done) => {
      const mockResult = {
        results: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 10
      };

      component.getPastSeasons(10, 1, 'asc', 'name').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne((request) => 
        request.url === '/api/Home/PastSeasons' &&
        request.params.get('sortDirection') === 'asc' &&
        request.params.get('sortColumn') === 'name'
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockResult);
    });

    it('should not include sort parameters when not provided', (done) => {
      const mockResult = {
        results: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 10
      };

      component.getPastSeasons(10, 1, undefined, undefined).subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne((request) => {
        return request.url === '/api/Home/PastSeasons' &&
               !request.params.has('sortDirection') &&
               !request.params.has('sortColumn');
      });
      req.flush(mockResult);
    });

    it('should update pastSeasons data source', (done) => {
      const mockEvents = [
        { seasonID: 4, name: 'Old Event', start: '2022-01-01', end: '2022-01-31', eventType: 'Draft' as const }
      ];
      const mockResult = {
        results: mockEvents,
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10
      };

      component.getPastSeasons(10, 1, undefined, undefined).subscribe({
        next: () => {
          expect(component.pastSeasons.data).toEqual(mockEvents);
          done();
        }
      });

      const req = httpTestingController.expectOne((request) => request.url === '/api/Home/PastSeasons');
      req.flush(mockResult);
    });

    it('should update total count when getting past seasons', (done) => {
      const mockResult = {
        results: [],
        totalCount: 42,
        pageNumber: 1,
        pageSize: 10
      };

      component.getPastSeasons(10, 1, undefined, undefined).subscribe({
        next: () => {
          expect(component.total()).toBe(42);
          done();
        }
      });

      const req = httpTestingController.expectOne((request) => request.url === '/api/Home/PastSeasons');
      req.flush(mockResult);
    });
  });

  describe('pagination and sorting', () => {
    it('should handle page size changes', (done) => {
      const mockResult = {
        results: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 25
      };

      component.getPastSeasons(25, 1, undefined, undefined).subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne((request) => 
        request.url === '/api/Home/PastSeasons' &&
        request.params.get('pageSize') === '25'
      );
      req.flush(mockResult);
    });

    it('should handle page number changes', (done) => {
      const mockResult = {
        results: [],
        totalCount: 0,
        pageNumber: 3,
        pageSize: 10
      };

      component.getPastSeasons(10, 3, undefined, undefined).subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne((request) => 
        request.url === '/api/Home/PastSeasons' &&
        request.params.get('pageNumber') === '3'
      );
      req.flush(mockResult);
    });

    it('should handle descending sort', (done) => {
      const mockResult = {
        results: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 10
      };

      component.getPastSeasons(10, 1, 'desc', 'name').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne((request) => 
        request.url === '/api/Home/PastSeasons' &&
        request.params.get('sortDirection') === 'desc' &&
        request.params.get('sortColumn') === 'name'
      );
      req.flush(mockResult);
    });
  });
});

describe('HomeComponent paginator and sort wiring', () => {
  let fixture: ComponentFixture<HomeComponent>;
  let component: HomeComponent;
  let http: HttpTestingController;

  const page = (names: string[], totalCount: number) => ({
    totalCount,
    results: names.map((name, i) => ({ seasonID: i + 1, name, start: '2025-01-01', end: '2025-02-01', eventType: 'Season' as const })),
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent, HttpClientTestingModule, NoopAnimationsModule],
      providers: [provideRouter([])]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    http.expectOne('/api/Home/CurrentAndFutureEvents/').flush([]);
    await settle();
  });

  afterEach(() => http.verify());

  async function settle(): Promise<void> {
    fixture.detectChanges();
    TestBed.tick();
    await Promise.resolve();
    fixture.detectChanges();
  }

  function pastSeasonsRequest() {
    const reqs = http.match(r => r.url === '/api/Home/PastSeasons');
    expect(reqs.length).withContext('one past-seasons request').toBeGreaterThan(0);
    return reqs[reqs.length - 1];
  }

  it('requests the first page of past seasons once the paginator is ready', async () => {
    const req = pastSeasonsRequest();
    expect(req.request.params.get('pageSize')).toBe('10');
    expect(req.request.params.get('pageNumber')).toBe('1');
    expect(req.request.params.has('sortColumn')).toBeFalse();
    req.flush(page(['Season 1', 'Season 2'], 12));
    fixture.detectChanges();

    expect(component.total()).toBe(12);
    expect(component.pastSeasons.data.map(e => e.name)).toEqual(['Season 1', 'Season 2']);
  });

  it('requests the next page when the paginator moves', async () => {
    pastSeasonsRequest().flush(page(['a'], 30));
    await settle();

    component.paginator()!.page.emit({ pageIndex: 2, pageSize: 20, length: 30 });
    await settle();

    const req = pastSeasonsRequest();
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('pageNumber')).toBe('3');
    req.flush(page(['c'], 30));
  });

  it('requests sorted results when a column header sort changes', async () => {
    pastSeasonsRequest().flush(page(['a'], 1));
    await settle();

    component.sort()!.sortChange.emit({ active: 'name', direction: 'desc' });
    await settle();

    const req = pastSeasonsRequest();
    expect(req.request.params.get('sortColumn')).toBe('name');
    expect(req.request.params.get('sortDirection')).toBe('desc');
    req.flush(page(['z'], 1));
  });
});
