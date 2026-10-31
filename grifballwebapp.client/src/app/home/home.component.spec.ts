import { ComponentFixture, ComponentFixtureAutoDetect } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { Event, eventLink, heroKicker, heroLede, HomeComponent, pickFeatured } from './home.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('HomeComponent', () => {
  let component: HomeComponent;
  let httpTestingController: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent, HttpClientTestingModule, NoopAnimationsModule],
      // These specs drive the component's methods by hand. Zoneless fixtures render on their own,
      // which would run ngOnInit and fire the real requests, so turn that off here.
      providers: [provideRouter([]), { provide: ComponentFixtureAutoDetect, useValue: false }]
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
        request.url === '/api/Season/GetSeasons' &&
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
        request.url === '/api/Season/GetSeasons' &&
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
        return request.url === '/api/Season/GetSeasons' &&
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

      const req = httpTestingController.expectOne((request) => request.url === '/api/Season/GetSeasons');
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

      const req = httpTestingController.expectOne((request) => request.url === '/api/Season/GetSeasons');
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
        request.url === '/api/Season/GetSeasons' &&
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
        request.url === '/api/Season/GetSeasons' &&
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
        request.url === '/api/Season/GetSeasons' &&
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
    const reqs = http.match(r => r.url === '/api/Season/GetSeasons');
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

describe('Home page events', () => {
  const now = Date.parse('2026-03-10T12:00:00Z');
  const ev = (eventType: Event['eventType'], start: string, end: string, seasonID = 4, name = 'Spring'): Event =>
    ({ seasonID, name, start, end, eventType });
  const signup = ev('Signup', '2026-03-01T00:00:00Z', '2026-03-20T00:00:00Z');
  const draft = ev('Draft', '2026-03-25T19:00:00Z', '2026-03-25T23:00:00Z');
  const season = ev('Season', '2026-02-01T00:00:00Z', '2026-05-01T00:00:00Z', 3, 'Winter');
  const later = ev('Season', '2026-04-01T00:00:00Z', '2026-06-01T00:00:00Z');

  it('features the event under way, signups before the draft before a season', () => {
    expect(pickFeatured([season, signup, draft], now)).toBe(signup);
    expect(pickFeatured([season, draft], now)).toBe(season);
  });

  it('features the next event to start when none is under way, and nothing without events', () => {
    expect(pickFeatured([later, draft], now)).toBe(draft);
    expect(pickFeatured([], now)).toBeNull();
  });

  it('says whether the featured event is under way or coming', () => {
    expect(heroKicker(signup, now)).toBe('Signups online');
    expect(heroKicker(signup, Date.parse('2026-02-01T00:00:00Z'))).toBe('Signups incoming');
    expect(heroKicker(draft, now)).toBe('Draft incoming');
    expect(heroKicker(draft, Date.parse('2026-03-25T20:00:00Z'))).toBe('Draft in progress');
    expect(heroKicker(season, now)).toBe('Season live');
    expect(heroKicker(later, now)).toBe('Season incoming');
  });

  it("describes the featured event, with the draft date for that season's signups", () => {
    const format = (iso: string) => iso.slice(0, 10);
    expect(heroLede(signup, [signup, draft], format)).toBe('Signups close 2026-03-20. Captains draft on 2026-03-25. Pick up a hammer.');
    expect(heroLede(signup, [signup, { ...draft, seasonID: 9 }], format)).toBe('Signups close 2026-03-20. Pick up a hammer.');
    expect(heroLede(draft, [], format)).toBe('The draft starts 2026-03-25 and runs until all picks are made.');
    expect(heroLede(season, [], format)).toBe('The season runs from 2026-02-01 to 2026-05-01.');
  });

  it('links each kind of event to its page', () => {
    expect(eventLink(signup)).toEqual(['/season', 4, 'signups']);
    expect(eventLink(draft)).toEqual(['/season', 4, 'teams']);
    expect(eventLink(season)).toEqual(['/season', 3]);
  });

  describe('rendered', () => {
    let fixture: ComponentFixture<HomeComponent>;
    let http: HttpTestingController;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [HomeComponent, HttpClientTestingModule, NoopAnimationsModule],
        providers: [provideRouter([])],
      }).compileComponents();
      fixture = TestBed.createComponent(HomeComponent);
      fixture.componentInstance.now.set(now);
      http = TestBed.inject(HttpTestingController);
    });

    const el = () => fixture.nativeElement as HTMLElement;
    const load = (events: Event[]) => {
      fixture.detectChanges();
      http.expectOne('/api/Home/CurrentAndFutureEvents/').flush(events);
      http.match(r => r.url === '/api/Season/GetSeasons').forEach(r => r.flush({ totalCount: 0, results: [] }));
      fixture.detectChanges();
    };

    it('puts the featured season in the hero with its calls to action, and every event in a card', () => {
      load([season, signup, draft]);
      expect(el().querySelector('h1')!.textContent).toBe('Spring');
      expect(el().querySelector('.hud-kicker')!.textContent).toContain('Signups online');
      const actions = Array.from(el().querySelectorAll('.home-hero__actions a')).map(a => [a.textContent!.trim(), a.getAttribute('href')]);
      expect(actions).toEqual([['Sign up', '/season/4/signupForm'], ['View roster', '/season/4/signups']]);
      const cards = Array.from(el().querySelectorAll('a.hud-card'));
      expect(cards.map(c => c.getAttribute('href'))).toEqual(['/season/3', '/season/4/signups', '/season/4/teams']);
      expect(cards.map(c => c.classList.contains('hud-card--active'))).toEqual([true, true, false]);
      expect(cards[2].textContent).toContain('Objective 03');
    });

    it('offers Sign up only while the featured signups are open, not before they open', () => {
      const upcoming = ev('Signup', '2026-03-15T00:00:00Z', '2026-03-20T00:00:00Z');
      load([upcoming]);
      expect(el().querySelector('.hud-kicker')!.textContent).toContain('Signups incoming');
      const actions = Array.from(el().querySelectorAll('.home-hero__actions a')).map(a => a.textContent!.trim());
      expect(actions).toEqual(['View roster']);

      fixture.componentInstance.now.set(Date.parse('2026-03-16T00:00:00Z'));
      fixture.detectChanges();
      expect(Array.from(el().querySelectorAll('.home-hero__actions a')).map(a => a.textContent!.trim())).toEqual(['Sign up', 'View roster']);
    });

    it('offers the draft or the season when that is the featured event', () => {
      load([draft]);
      expect(Array.from(el().querySelectorAll('.home-hero__actions a')).map(a => a.textContent!.trim())).toEqual(['Watch the draft', 'Season overview']);
      fixture.componentInstance.currentAndFutureEvents.set([later]);
      fixture.detectChanges();
      const links = Array.from(el().querySelectorAll('.home-hero__actions a')).map(a => a.getAttribute('href'));
      expect(links).toEqual(['/season/4', '/season/4?tab=schedule']);
    });

    it('keeps the page hidden until the events are in, then shows it', () => {
      fixture.detectChanges();
      const page = () => el().querySelector('.hud-page')!;
      expect(page().classList).toContain('hud-waiting');
      http.expectOne('/api/Home/CurrentAndFutureEvents/').flush('x', { status: 500, statusText: 'Server Error' });
      http.match(r => r.url === '/api/Season/GetSeasons').forEach(r => r.flush({ totalCount: 0, results: [] }));
      fixture.detectChanges();
      expect(page().classList).not.toContain('hud-waiting');
      expect(el().textContent).toContain('There are no current or upcoming events');
    });

    it('says so when there are no events', () => {
      load([]);
      expect(el().textContent).toContain('There are no current or upcoming events');
      expect(el().querySelector('a.hud-card')).toBeNull();
    });
  });
});
