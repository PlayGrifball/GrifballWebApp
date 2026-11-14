import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { seasonInfo } from '@shared/testing/seasonInfo.stub';
import { HomeStore } from './home.store';

describe('HomeStore', () => {
  let store: HomeStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [HomeStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(HomeStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('is done with the events even when they fail', () => {
    store.loadEvents();
    http.expectOne('/api/events').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.eventsLoaded()).toBeTrue();
    expect(store.events()).toEqual([]);
  });

  it('loads the current and coming events', () => {
    const events = [{ seasonID: 1, name: 'Test Event', start: '2024-01-01', end: '2024-01-31', eventType: 'Signup' as const }];
    store.loadEvents();
    const req = http.expectOne('/api/events');
    expect(req.request.method).toBe('GET');
    req.flush(events);
    expect(store.events()).toEqual(events);
    expect(store.eventsLoaded()).toBeTrue();
  });

  describe('loadPastSeasons', () => {
    it('should make GET request with pagination parameters', (done) => {
      const mockResult = {
        results: [
          { ...seasonInfo(3, { seasonName: 'Past Event', start: '2023-01-01', end: '2023-01-31' }), teamCount: 0, playerCount: 0, champions: [], teamNames: [] }
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10
      };

      store.loadPastSeasons(10, 1, undefined, undefined).subscribe({
        next: (result) => {
          expect(result).toEqual(mockResult);
          expect(store.total()).toBe(1);
          expect(store.pastSeasons()).toEqual(mockResult.results);
          done();
        }
      });

      const req = http.expectOne((request) => 
        request.url === '/api/seasons' &&
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

      store.loadPastSeasons(10, 1, 'asc', 'seasonName').subscribe({
        next: () => done()
      });

      const req = http.expectOne((request) => 
        request.url === '/api/seasons' &&
        request.params.get('sortDirection') === 'Asc' &&
        request.params.get('sortColumn') === 'seasonName'
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

      store.loadPastSeasons(10, 1, undefined, undefined).subscribe({
        next: result => {
          expect(result).toEqual(mockResult);
          done();
        }
      });

      const req = http.expectOne((request) => {
        return request.url === '/api/seasons' &&
               !request.params.has('sortDirection') &&
               !request.params.has('sortColumn');
      });
      req.flush(mockResult);
    });

    it('should update pastSeasons data source', (done) => {
      const mockEvents = [
        { ...seasonInfo(4, { seasonName: 'Old Event', start: '2022-01-01', end: '2022-01-31' }), teamCount: 0, playerCount: 0, champions: [], teamNames: [] }
      ];
      const mockResult = {
        results: mockEvents,
        totalCount: 1,
        pageNumber: 1,
        pageSize: 10
      };

      store.loadPastSeasons(10, 1, undefined, undefined).subscribe({
        next: () => {
          expect(store.pastSeasons()).toEqual(mockEvents);
          done();
        }
      });

      const req = http.expectOne((request) => request.url === '/api/seasons');
      req.flush(mockResult);
    });

    it('should update total count when getting past seasons', (done) => {
      const mockResult = {
        results: [],
        totalCount: 42,
        pageNumber: 1,
        pageSize: 10
      };

      store.loadPastSeasons(10, 1, undefined, undefined).subscribe({
        next: () => {
          expect(store.total()).toBe(42);
          done();
        }
      });

      const req = http.expectOne((request) => request.url === '/api/seasons');
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

      store.loadPastSeasons(25, 1, undefined, undefined).subscribe({
        next: result => {
          expect(result).toEqual(mockResult);
          done();
        }
      });

      const req = http.expectOne((request) => 
        request.url === '/api/seasons' &&
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

      store.loadPastSeasons(10, 3, undefined, undefined).subscribe({
        next: result => {
          expect(result).toEqual(mockResult);
          done();
        }
      });

      const req = http.expectOne((request) => 
        request.url === '/api/seasons' &&
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

      store.loadPastSeasons(10, 1, 'desc', 'seasonName').subscribe({
        next: result => {
          expect(result).toEqual(mockResult);
          done();
        }
      });

      const req = http.expectOne((request) => 
        request.url === '/api/seasons' &&
        request.params.get('sortDirection') === 'Desc' &&
        request.params.get('sortColumn') === 'seasonName'
      );
      req.flush(mockResult);
    });
  });
});
