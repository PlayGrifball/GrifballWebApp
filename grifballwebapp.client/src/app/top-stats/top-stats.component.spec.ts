import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TopStatsComponent } from './top-stats.component';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { KillsDto } from '../api/dtos/killsDto';

describe('TopStatsComponent', () => {
  let component: TopStatsComponent;
  let fixture: ComponentFixture<TopStatsComponent>;
  let mockApiClient: jasmine.SpyObj<ApiClientService>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    mockApiClient = jasmine.createSpyObj('ApiClientService', ['getKills']);
    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open']);

    await TestBed.configureTestingModule({
      imports: [TopStatsComponent],
      providers: [
        { provide: ApiClientService, useValue: mockApiClient },
        { provide: MatSnackBar, useValue: mockSnackBar }
      ]
    }).compileComponents();

    mockApiClient.getKills.and.returnValue(of([]));
    fixture = TestBed.createComponent(TopStatsComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have empty kills array initially', () => {
    expect(component.kills).toEqual([]);
  });

  it('should have correct displayed columns', () => {
    expect(component.displayedColumns).toEqual(['rank', 'gamertag', 'kills']);
  });

  it('should call getKills on init', () => {
    fixture.detectChanges();

    expect(mockApiClient.getKills).toHaveBeenCalled();
  });

  it('should populate kills array with data from API', () => {
    const mockKills: KillsDto[] = [
      { rank: 1, gamertag: 'Player1', kills: 100 },
      { rank: 2, gamertag: 'Player2', kills: 90 }
    ];
    mockApiClient.getKills.and.returnValue(of(mockKills));

    component.getKills();

    expect(component.kills).toEqual(mockKills);
  });

  it('should handle empty kills array from API', () => {
    mockApiClient.getKills.and.returnValue(of([]));

    component.getKills();

    expect(component.kills).toEqual([]);
  });

  it('should log error when API call fails', () => {
    spyOn(console, 'error');
    const error = new Error('API Error');
    mockApiClient.getKills.and.returnValue(throwError(() => error));

    component.getKills();

    expect(console.error).toHaveBeenCalledWith(error);
  });

  it('should handle large number of kills', () => {
    const mockKills: KillsDto[] = Array.from({ length: 100 }, (_, i) => ({
      rank: i + 1,
      gamertag: `Player${i + 1}`,
      kills: 1000 - (i * 10)
    }));
    mockApiClient.getKills.and.returnValue(of(mockKills));

    component.getKills();

    expect(component.kills.length).toBe(100);
    expect(component.kills[0].rank).toBe(1);
    expect(component.kills[99].rank).toBe(100);
  });

  it('should not open snack bar on successful completion', () => {
    const mockKills: KillsDto[] = [
      { rank: 1, gamertag: 'Player1', kills: 100 }
    ];
    mockApiClient.getKills.and.returnValue(of(mockKills));

    component.getKills();

    // The complete handler is commented out, so snackBar.open should not be called
    expect(mockSnackBar.open).not.toHaveBeenCalled();
  });

  it('should update kills when getKills is called multiple times', () => {
    const firstKills: KillsDto[] = [{ rank: 1, gamertag: 'Player1', kills: 50 }];
    const secondKills: KillsDto[] = [{ rank: 1, gamertag: 'Player2', kills: 100 }];
    
    mockApiClient.getKills.and.returnValue(of(firstKills));
    component.getKills();
    expect(component.kills).toEqual(firstKills);

    mockApiClient.getKills.and.returnValue(of(secondKills));
    component.getKills();
    expect(component.kills).toEqual(secondKills);
  });
});
