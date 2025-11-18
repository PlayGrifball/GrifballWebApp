import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlayerGradesComponent } from './playerGrades.component';
import { ActivatedRoute } from '@angular/router';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { GradesDto, PerMinuteDto, LetterDto } from './gradesDto';
import { DecimalPipe } from '@angular/common';

describe('PlayerGradesComponent', () => {
  let component: PlayerGradesComponent;
  let fixture: ComponentFixture<PlayerGradesComponent>;
  let httpTestingController: HttpTestingController;
  let mockActivatedRoute: any;

  beforeEach(async () => {
    mockActivatedRoute = {
      snapshot: {
        paramMap: {
          get: jasmine.createSpy('get').and.returnValue('7')
        }
      }
    };

    await TestBed.configureTestingModule({
      imports: [PlayerGradesComponent, HttpClientTestingModule],
      providers: [
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        DecimalPipe
      ]
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlayerGradesComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty pm and letters arrays', () => {
    expect(component.pm).toEqual([]);
    expect(component.letters).toEqual([]);
  });

  it('should get seasonID from route params on init', () => {
    mockActivatedRoute.snapshot.paramMap.get.and.returnValue('7');

    fixture.detectChanges();

    const req = httpTestingController.expectOne('/api/Grades/GetGrades/7');
    req.flush({ totals: [], perMinutes: [], letters: [] });

    expect(mockActivatedRoute.snapshot.paramMap.get).toHaveBeenCalledWith('seasonID');
  });

  describe('getGrades', () => {
    it('should fetch grades from API', () => {
      const mockGrades: GradesDto = {
        totals: [], perMinutes: [
          {
            xboxUserID: 1, gamertag: 'Player1',
            goalsPM: 0.5,
            kdSpreadPM: 1.2,
            punchesPM: 2.1,
            spreesPM: 0.3,
            doubleKillsPM: 0.4,
            tripleKillsPM: 0.1,
            multiKillsPM: 0.5,
            xFactorPM: 3.5,
            killsPM: 5.2
          }
        ],
        letters: [
          {
            xboxUserID: 1, gamertag: 'Player1',
            goals: 'A',
            kdSpread: 'B+',
            punches: 'S',
            sprees: 'C',
            doubleKills: 'B',
            tripleKills: 'A-',
            multiKills: 'A+',
            xFactor: 'S+',
            kills: 'A',
            gradeAvgMath: 3.75,
            gradeAvg: 'A'
          }
        ]
      };

      component.getGrades();

      const req = httpTestingController.expectOne((request) => 
        request.url.includes('/api/Grades/GetGrades/')
      );
      expect(req.request.method).toBe('GET');
      req.flush(mockGrades);

      expect(component.pm).toEqual(mockGrades.perMinutes);
      expect(component.letters).toEqual(mockGrades.letters);
    });

    it('should handle empty grades response', () => {
      component.getGrades();

      const req = httpTestingController.expectOne((request) => 
        request.url.includes('/api/Grades/GetGrades/')
      );
      req.flush({ totals: [], perMinutes: [], letters: [] });

      expect(component.pm).toEqual([]);
      expect(component.letters).toEqual([]);
    });

    it('should handle multiple players', () => {
      const mockGrades: GradesDto = {
        totals: [], perMinutes: [
          { xboxUserID: 1, gamertag: 'Player1', goalsPM: 1, kdSpreadPM: 1, punchesPM: 1, spreesPM: 1, doubleKillsPM: 1, tripleKillsPM: 1, multiKillsPM: 1, xFactorPM: 1, killsPM: 1 },
          { xboxUserID: 1, gamertag: 'Player2', goalsPM: 2, kdSpreadPM: 2, punchesPM: 2, spreesPM: 2, doubleKillsPM: 2, tripleKillsPM: 2, multiKillsPM: 2, xFactorPM: 2, killsPM: 2 },
          { xboxUserID: 1, gamertag: 'Player3', goalsPM: 3, kdSpreadPM: 3, punchesPM: 3, spreesPM: 3, doubleKillsPM: 3, tripleKillsPM: 3, multiKillsPM: 3, xFactorPM: 3, killsPM: 3 }
        ],
        letters: []
      };

      component.getGrades();

      const req = httpTestingController.expectOne((request) => 
        request.url.includes('/api/Grades/GetGrades/')
      );
      req.flush(mockGrades);

      expect(component.pm.length).toBe(3);
      expect(component.pm[1].gamertag).toBe('Player2');
    });
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
      expect(component.getColor('X' as any)).toBe('');
    });

    it('should handle all grade variations', () => {
      const grades = ['S+', 'S', 'S-', 'A+', 'A', 'A-', 'B+', 'B', 'B-', 'C+', 'C', 'C-', 'D+', 'D', 'D-', 'E+', 'E', 'E-', 'F+', 'F', 'F-'];
      
      grades.forEach(grade => {
        const color = component.getColor(grade as any);
        expect(color).toBeTruthy();
        expect(color.startsWith('#')).toBe(true);
      });
    });
  });

  describe('column definitions', () => {
    it('should have correct pm displayed columns', () => {
      expect(component.pmDisplayedColumns).toEqual([
        'gamertag', 'goals', 'kdSpread', 'punches', 'sprees', 
        'doubleKills', 'tripleKills', 'multiKills', 'xFactor', 'kills'
      ]);
    });

    it('should have correct letter displayed columns', () => {
      expect(component.letterDisplayedColumns).toEqual([
        'gamertag', 'goals', 'kdSpread', 'punches', 'sprees',
        'doubleKills', 'tripleKills', 'multiKills', 'xFactor', 'kills',
        'gradeAvgMath', 'gradeAvg'
      ]);
    });

    it('should have pmColumns with correct structure', () => {
      expect(component.pmColumns.length).toBe(10);
      expect(component.pmColumns[0].columnDef).toBe('gamertag');
      expect(component.pmColumns[0].header).toBe('Gamertag');
    });

    it('should have letterColumns with correct structure', () => {
      expect(component.letterColumns.length).toBe(12);
      expect(component.letterColumns[0].columnDef).toBe('gamertag');
      expect(component.letterColumns[0].header).toBe('Gamertag');
    });

    it('should format pmColumn cells correctly', () => {
      const mockData: PerMinuteDto = {
        xboxUserID: 1, gamertag: 'TestPlayer',
        goalsPM: 1.5,
        kdSpreadPM: 2.3,
        punchesPM: 0.8,
        spreesPM: 0.5,
        doubleKillsPM: 0.3,
        tripleKillsPM: 0.1,
        multiKillsPM: 0.4,
        xFactorPM: 3.2,
        killsPM: 5.1
      };

      expect(component.pmColumns[0].cell(mockData)).toBe('TestPlayer');
      expect(component.pmColumns[1].cell(mockData)).toBe('1.5');
      expect(component.pmColumns[9].cell(mockData)).toBe('5.1');
    });

    it('should format letterColumn cells correctly', () => {
      const mockData: LetterDto = {
        xboxUserID: 1, gamertag: 'TestPlayer',
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
        xboxUserID: 1, gamertag: 'TestPlayer',
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

  describe('test property', () => {
    it('should have test property set to black', () => {
      expect(component.test).toBe('black');
    });
  });

  describe('integration tests', () => {
    it('should call getGrades on initialization', () => {
      spyOn(component, 'getGrades');

      component.ngOnInit();

      expect(component.getGrades).toHaveBeenCalled();
    });

    it('should handle API with both perMinutes and letters', () => {
      const mockGrades: GradesDto = {
        totals: [], perMinutes: [
          { xboxUserID: 1, gamertag: 'Player1', goalsPM: 0.5, kdSpreadPM: 1.2, punchesPM: 2.1, spreesPM: 0.3, doubleKillsPM: 0.4, tripleKillsPM: 0.1, multiKillsPM: 0.5, xFactorPM: 3.5, killsPM: 5.2 }
        ],
        letters: [
          { xboxUserID: 1, gamertag: 'Player1', goals: 'A', kdSpread: 'B+', punches: 'S', sprees: 'C', doubleKills: 'B', tripleKills: 'A-', multiKills: 'A+', xFactor: 'S+', kills: 'A', gradeAvgMath: 3.75, gradeAvg: 'A' }
        ]
      };

      component.getGrades();

      const req = httpTestingController.expectOne((request) => 
        request.url.includes('/api/Grades/GetGrades/')
      );
      req.flush(mockGrades);

      expect(component.pm.length).toBe(1);
      expect(component.letters.length).toBe(1);
      expect(component.pm[0].gamertag).toBe('Player1');
      expect(component.letters[0].gamertag).toBe('Player1');
    });
  });
});
