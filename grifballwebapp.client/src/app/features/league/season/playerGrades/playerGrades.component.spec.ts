import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlayerGradesComponent } from './playerGrades.component';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { GradesDto, LetterDto } from '@api/models';
import { DecimalPipe } from '@angular/common';
import { Observable, of, Subject, throwError } from 'rxjs';
import { SeasonSummaryService } from '../seasonSummary';
import type { SeasonSummaryDto } from '@api/models';

describe('PlayerGradesComponent', () => {
  let component: PlayerGradesComponent;
  let fixture: ComponentFixture<PlayerGradesComponent>;
  let httpTestingController: HttpTestingController;
  let summary: Observable<Partial<SeasonSummaryDto>>;

  beforeEach(async () => {
    summary = of({ isHistorical: false, hasEnded: false, matchCount: 0, bracketMatchCount: 0 });

    await TestBed.configureTestingModule({
      imports: [PlayerGradesComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SeasonSummaryService, useValue: { get: () => summary } },
        DecimalPipe
      ]
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlayerGradesComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonID', 7);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  const page = () => fixture.nativeElement as HTMLElement;
  const empty = { totals: [], perMinutes: [], letters: [] };
  const grade: LetterDto = { xboxUserID: 1, userID: null, gamertag: 'Grif', goals: 'A', kdSpread: 'A', punches: 'A', sprees: 'A', doubleKills: 'A',
    tripleKills: 'A', multiKills: 'A', xFactor: 'A', kills: 'A', gradeAvgMath: 3, gradeAvg: 'A' };

  it('says a backfilled season without matches has no grades, with its spreadsheet, and has no table', () => {
    summary = of({ isHistorical: true, hasEnded: true, matchCount: 0, bracketMatchCount: 0, historicalSourceUrl: 'https://docs.google.com/spreadsheets/d/x' });
    fixture.detectChanges();
    // Nothing to grade: the grades aren't asked for
    httpTestingController.expectNone('/api/seasons/7/grades');
    const notice = page().querySelector('app-data-notice')!;
    expect(notice.textContent).toContain('Player grades come from');
    expect(notice.querySelector('a')!.getAttribute('href')).toBe('https://docs.google.com/spreadsheets/d/x');
    expect(page().querySelector('table')).toBeNull();
  });

  it('says an ended season of the app\'s own without matches has no grades, and has no table', () => {
    summary = of({ isHistorical: false, hasEnded: true, matchCount: 0, bracketMatchCount: 0, historicalSourceUrl: null });
    fixture.detectChanges();
    httpTestingController.expectNone('/api/seasons/7/grades');
    const notice = page().querySelector('app-data-notice')!;
    expect(notice.textContent).toContain('none were recorded for this season');
    expect(notice.querySelector('a')).toBeNull();
    expect(page().querySelector('table')).toBeNull();
  });

  it('says an ended season with a schedule but no Halo Infinite games has no grades, and has no table', () => {
    summary = of({ isHistorical: false, hasEnded: true, matchCount: 12, bracketMatchCount: 3, historicalSourceUrl: null });
    fixture.detectChanges();
    httpTestingController.expectOne('/api/seasons/7/grades').flush(empty);
    fixture.detectChanges();
    expect(page().querySelector('app-data-notice')!.textContent).toContain('none were recorded for this season');
    expect(page().querySelector('table')).toBeNull();
  });

  it('shows neither the table nor the notice until the summary has loaded', () => {
    const pending = new Subject<Partial<SeasonSummaryDto>>();
    summary = pending;
    fixture.detectChanges();
    httpTestingController.expectNone('/api/seasons/7/grades');
    expect(page().querySelector('table')).toBeNull();
    expect(page().querySelector('app-data-notice')).toBeNull();

    pending.next({ isHistorical: true, hasEnded: true, matchCount: 0, bracketMatchCount: 0 });
    fixture.detectChanges();
    expect(page().querySelector('app-data-notice')).not.toBeNull();
    expect(page().querySelector('table')).toBeNull();
  });

  it('shows neither the table nor the notice until the grades have loaded', () => {
    summary = of({ isHistorical: true, hasEnded: true, matchCount: 4, bracketMatchCount: 0 });
    fixture.detectChanges();
    const req = httpTestingController.expectOne('/api/seasons/7/grades');
    expect(page().querySelector('table')).toBeNull();
    expect(page().querySelector('app-data-notice')).toBeNull();

    req.flush({ ...empty, letters: [grade] });
    fixture.detectChanges();
    expect(page().querySelector('table')).not.toBeNull();
    expect(page().querySelector('app-data-notice')).toBeNull();
  });

  it('says a season still going without games yet has no grades yet, inside the table', () => {
    fixture.detectChanges();
    httpTestingController.expectOne('/api/seasons/7/grades').flush(empty);
    fixture.detectChanges();
    expect(page().querySelector('app-data-notice')).toBeNull();
    expect(page().querySelector('table')).not.toBeNull();
    const row = page().querySelector('tr.grades-empty td')!;
    expect(row.textContent).toContain('No grades yet');
    expect(row.getAttribute('colspan')).toBe('12');
    // Material marks the no-data row as a row too: no grade rows besides it
    expect(page().querySelectorAll('tr.mat-mdc-row:not(.grades-empty)')).toHaveSize(0);
  });

  it('says the grades could not be loaded, inside the table, when they fail', () => {
    summary = of({ isHistorical: true, hasEnded: true, matchCount: 4, bracketMatchCount: 0 });
    fixture.detectChanges();
    httpTestingController.expectOne('/api/seasons/7/grades').flush('down', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    // A failure isn't proof the season has no games: no notice
    expect(page().querySelector('app-data-notice')).toBeNull();
    const row = page().querySelector('tr.grades-empty td')!;
    expect(row.textContent).toContain('couldn\'t be loaded');
    // Announced: the cell's text is an alert (a cell itself can't be one)
    expect(row.querySelector('[role="alert"]')?.textContent).toContain('couldn\'t be loaded');
  });

  it('shows the table for a season with graded games, or when the season cannot be loaded', () => {
    summary = of({ isHistorical: true, hasEnded: true, matchCount: 4, bracketMatchCount: 0 });
    fixture.detectChanges();
    httpTestingController.expectOne('/api/seasons/7/grades').flush({ ...empty, letters: [grade] });
    fixture.detectChanges();
    expect(page().querySelector('app-data-notice')).toBeNull();
    expect(page().querySelector('table tr.mat-mdc-row td')!.textContent!.trim()).toBe('Grif');

    summary = throwError(() => new Error('down'));
    const other = TestBed.createComponent(PlayerGradesComponent);
    other.componentRef.setInput('seasonID', 7);
    other.detectChanges();
    httpTestingController.expectOne('/api/seasons/7/grades').flush(empty);
    other.detectChanges();
    expect((other.nativeElement as HTMLElement).querySelector('app-data-notice')).toBeNull();
    expect((other.nativeElement as HTMLElement).querySelector('table')).not.toBeNull();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty letters and nothing loaded', () => {
    expect(component.letters()).toEqual([]);
    expect(component.gradesDone()).toBeFalse();
    expect(component.showTable()).toBeFalse();
  });

  it('loads the grades of the season in the route', () => {
    fixture.detectChanges();

    httpTestingController.expectOne('/api/seasons/7/grades').flush({ totals: [], perMinutes: [], letters: [] });
    expect(component.seasonID()).toBe(7);
  });

  it("replaces the grades with the new season's when the route moves to another season", () => {
    summary = of({ isHistorical: false, hasEnded: false, matchCount: 4, bracketMatchCount: 0 });
    fixture.detectChanges();
    httpTestingController.expectOne('/api/seasons/7/grades').flush({ totals: [], perMinutes: [], letters: [grade] });
    expect(component.letters()).toEqual([grade]);

    fixture.componentRef.setInput('seasonID', 8);
    fixture.detectChanges();
    // Nothing of season 7 while season 8's load
    expect(component.letters()).toEqual([]);
    expect(component.showTable()).toBeFalse();

    const other = { ...grade, gamertag: 'Simmons' };
    httpTestingController.expectOne('/api/seasons/8/grades').flush({ totals: [], perMinutes: [], letters: [other] });
    fixture.detectChanges();
    expect(component.letters()).toEqual([other]);
    expect(page().querySelector('app-season-tabs a')!.getAttribute('href')).toBe('/season/8');
  });

  it("drops the previous season's grades when they arrive late", () => {
    summary = of({ isHistorical: false, hasEnded: false, matchCount: 4, bracketMatchCount: 0 });
    fixture.detectChanges();
    const late = httpTestingController.expectOne('/api/seasons/7/grades');

    fixture.componentRef.setInput('seasonID', 8);
    fixture.detectChanges();
    httpTestingController.expectOne('/api/seasons/8/grades').flush({ totals: [], perMinutes: [], letters: [] });
    late.flush({ totals: [], perMinutes: [], letters: [grade] });

    expect(component.letters()).toEqual([]);
  });

  describe('getColor', () => {
    it('should return gold for S+ grade', () => {
      expect(component.getColor('S+')).toBe('#FFD700');
    });

    it('should return gold for S grade', () => {
      expect(component.getColor('S')).toBe('#FFD700');
    });

    it('should return gold for S- grade', () => {
      expect(component.getColor('S-')).toBe('#FFD700');
    });

    it('should return light blue for A+ grade', () => {
      expect(component.getColor('A+')).toBe('#C9DAF8');
    });

    it('should return light blue for A grade', () => {
      expect(component.getColor('A')).toBe('#C9DAF8');
    });

    it('should return light blue for A- grade', () => {
      expect(component.getColor('A-')).toBe('#C9DAF8');
    });

    it('should return purple for B+ grade', () => {
      expect(component.getColor('B+')).toBe('#D9D2E9');
    });

    it('should return purple for B grade', () => {
      expect(component.getColor('B')).toBe('#D9D2E9');
    });

    it('should return purple for B- grade', () => {
      expect(component.getColor('B-')).toBe('#D9D2E9');
    });

    it('should return green for C+ grade', () => {
      expect(component.getColor('C+')).toBe('#B6D7A8');
    });

    it('should return green for C grade', () => {
      expect(component.getColor('C')).toBe('#B6D7A8');
    });

    it('should return green for C- grade', () => {
      expect(component.getColor('C-')).toBe('#B6D7A8');
    });

    it('should return blue for D+ grade', () => {
      expect(component.getColor('D+')).toBe('#6FA8DC');
    });

    it('should return blue for D grade', () => {
      expect(component.getColor('D')).toBe('#6FA8DC');
    });

    it('should return blue for D- grade', () => {
      expect(component.getColor('D-')).toBe('#6FA8DC');
    });

    it('should return pink for E+ grade', () => {
      expect(component.getColor('E+')).toBe('#C27BA0');
    });

    it('should return pink for E grade', () => {
      expect(component.getColor('E')).toBe('#C27BA0');
    });

    it('should return pink for E- grade', () => {
      expect(component.getColor('E-')).toBe('#C27BA0');
    });

    it('should return red for F+ grade', () => {
      expect(component.getColor('F+')).toBe('#A61C00');
    });

    it('should return red for F grade', () => {
      expect(component.getColor('F')).toBe('#A61C00');
    });

    it('should return red for F- grade', () => {
      expect(component.getColor('F-')).toBe('#A61C00');
    });

    it('should return empty string for invalid grade', () => {
      expect(component.getColor('X')).toBe('');
    });

    it('should handle all grade variations', () => {
      const grades = ['S+', 'S', 'S-', 'A+', 'A', 'A-', 'B+', 'B', 'B-', 'C+', 'C', 'C-', 'D+', 'D', 'D-', 'E+', 'E', 'E-', 'F+', 'F', 'F-'];
      
      grades.forEach(grade => {
        const color = component.getColor(grade);
        expect(color).toBeTruthy();
        expect(color.startsWith('#')).toBe(true);
      });
    });
  });

  describe('column definitions', () => {
    it('should have correct letter displayed columns', () => {
      expect(component.letterDisplayedColumns).toEqual([
        'gamertag', 'goals', 'kdSpread', 'punches', 'sprees',
        'doubleKills', 'tripleKills', 'multiKills', 'xFactor', 'kills',
        'gradeAvgMath', 'gradeAvg'
      ]);
    });

    it('should have letterColumns with correct structure', () => {
      expect(component.letterColumns).toHaveSize(12);
      expect(component.letterColumns[0].columnDef).toBe('gamertag');
      expect(component.letterColumns[0].header).toBe('Gamertag');
    });

    it('should format letterColumn cells correctly', () => {
      const mockData: LetterDto = {
        xboxUserID: 1, userID: null, gamertag: 'TestPlayer',
        goals: 'A',
        kdSpread: 'B',
        punches: 'S',
        sprees: 'C',
        doubleKills: 'A+',
        tripleKills: 'B-',
        multiKills: 'A-',
        xFactor: 'S+',
        kills: 'A',
        gradeAvgMath: 3.75,
        gradeAvg: 'A'
      };

      expect(component.letterColumns[0].cell(mockData)).toBe('TestPlayer');
      expect(component.letterColumns[1].cell(mockData)).toBe('A');
    });

    it('should apply colors to letter columns', () => {
      const mockData: LetterDto = {
        xboxUserID: 1, userID: null, gamertag: 'TestPlayer',
        goals: 'S+',
        kdSpread: 'A',
        punches: 'B',
        sprees: 'C',
        doubleKills: 'D',
        tripleKills: 'E',
        multiKills: 'F',
        xFactor: 'A+',
        kills: 'S',
        gradeAvgMath: 3.0,
        gradeAvg: 'A'
      };

      expect(component.letterColumns[1].style(mockData)).toBe('#FFD700'); // S+ is gold
      expect(component.letterColumns[2].style(mockData)).toBe('#C9DAF8'); // A is light blue
    });
  });


  describe('integration tests', () => {
    it('asks for the grades on initialization', () => {
      fixture.detectChanges();

      httpTestingController.expectOne('/api/seasons/7/grades').flush(empty);
      expect(component.gradesDone()).toBeTrue();
    });

    it('should handle API with both perMinutes and letters', () => {
      const mockGrades: GradesDto = {
        totals: [], perMinutes: [
          { xboxUserID: 1, gamertag: 'Player1', goalsPM: 0.5, kdSpreadPM: 1.2, punchesPM: 2.1, spreesPM: 0.3, doubleKillsPM: 0.4, tripleKillsPM: 0.1, multiKillsPM: 0.5, xFactorPM: 3.5, killsPM: 5.2 }
        ],
        letters: [
          { xboxUserID: 1, userID: null, gamertag: 'Player1', goals: 'A', kdSpread: 'B+', punches: 'S', sprees: 'C', doubleKills: 'B', tripleKills: 'A-', multiKills: 'A+', xFactor: 'S+', kills: 'A', gradeAvgMath: 3.75, gradeAvg: 'A' }
        ]
      };

      fixture.detectChanges();

      const req = httpTestingController.expectOne((request) => 
        /^\/api\/seasons\/\d+\/grades$/.test(request.url)
      );
      req.flush(mockGrades);

      expect(component.letters()).toHaveSize(1);
      expect(component.letters()[0].gamertag).toBe('Player1');
    });
  });

  it('renders the grades once they arrive, without a manual detectChanges', async () => {
    await fixture.whenStable();
    httpTestingController.expectOne('/api/seasons/7/grades').flush({
      perMinutes: [],
      letters: [{ xboxUserID: 1, gamertag: 'Grif', goals: 'S+', kdSpread: 'A', punches: 'B-', sprees: 'C', doubleKills: 'D+',
        tripleKills: 'E', multiKills: 'F-', xFactor: 'A+', kills: 'S', gradeAvgMath: 3, gradeAvg: 'B+' }],
    });
    await fixture.whenStable();

    const firstCell = (fixture.nativeElement as HTMLElement).querySelector('tr.mat-mdc-row td');
    expect(firstCell?.textContent?.trim()).toBe('Grif');
  });
});

describe('PlayerGradesComponent rendering', () => {
  let fixture: ComponentFixture<PlayerGradesComponent>;
  let component: PlayerGradesComponent;
  let http: HttpTestingController;

  const letter: LetterDto = {
    xboxUserID: 1, userID: null, gamertag: 'Grif',
    goals: 'S+', kdSpread: 'A', punches: 'B-', sprees: 'C', doubleKills: 'D+',
    tripleKills: 'E', multiKills: 'F-', xFactor: 'A+', kills: 'S',
    gradeAvgMath: 3, gradeAvg: 'B+'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlayerGradesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: SeasonSummaryService, useValue: { get: () => of({ isHistorical: false, hasEnded: false, matchCount: 1, bracketMatchCount: 0 }) } }]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlayerGradesComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonID', 7);
    fixture.detectChanges();
    http.expectOne('/api/seasons/7/grades').flush({ totals: [], perMinutes: [], letters: [letter] });
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const cells = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row td')) as HTMLElement[];

  it('defines a column for every displayed column, in order', () => {
    expect(component.letterColumns.map(c => c.columnDef)).toEqual(component.letterDisplayedColumns);
  });


  it('renders one cell per grade with the average to two decimals', () => {
    expect(cells().map(c => c.textContent!.trim())).toEqual(
      ['Grif', 'S+', 'A', 'B-', 'C', 'D+', 'E', 'F-', 'A+', 'S', '3.00', 'B+']);
  });

  it('colours each grade cell by its letter and leaves the gamertag plain', () => {
    const toHex = (c: HTMLElement) => {
      const m = /rgb\((\d+), (\d+), (\d+)\)/.exec(c.style.background);
      return m ? '#' + m.slice(1).map(n => Number(n).toString(16).padStart(2, '0')).join('').toUpperCase() : '';
    };
    expect(cells().map(toHex)).toEqual([
      '', '#FFD700', '#C9DAF8', '#D9D2E9', '#B6D7A8', '#6FA8DC', '#C27BA0', '#A61C00', '#C9DAF8', '#FFD700',
      '#D9D2E9', '#D9D2E9'
    ]);
    expect(cells()[1].style.color).toBe('black');
    expect(cells()[0].style.color).toBe('');
  });
});
