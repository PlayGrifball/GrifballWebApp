import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { HistoricalTeamComponent, HistoricalTeamDetailDto } from './historical-team.component';

describe('HistoricalTeamComponent', () => {
  let fixture: ComponentFixture<HistoricalTeamComponent>;
  let component: HistoricalTeamComponent;
  let http: HttpTestingController;

  const mockTeam: HistoricalTeamDetailDto = {
    teamName: 'Relentless',
    teamSlug: 'relentless',
    totalChampionships: 2,
    championships: [
      {
        seasonID: 15,
        seasonName: 'Summer League 2020',
        year: 2020,
        leagueName: 'AGLA',
        gameVersionName: 'Halo 3',
        tier: 'Pro',
        roster: [{ userID: 101, gamertag: 'Veritas', isCaptain: true }]
      }
    ],
    seasons: [
      {
        seasonID: 15,
        seasonName: 'Summer League 2020',
        year: 2020,
        leagueName: 'AGLA',
        gameVersionName: 'Halo 3',
        gamesPlayed: 14,
        wins: 12,
        losses: 2,
        goals: 42,
        wonChampionship: true,
        roster: [
          {
            userID: 101,
            gamertag: 'Veritas',
            stintIndex: 1,
            gamesPlayed: 14,
            goals: 28,
            kills: 180,
            deaths: 90,
            carrierKills: 40
          }
        ]
      }
    ],
    allTimePlayers: [
      {
        userID: 101,
        gamertag: 'Veritas',
        seasonsPlayed: 3,
        gamesPlayed: 38,
        goals: 75,
        championshipsWon: 2
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HistoricalTeamComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'relentless' } } } }
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HistoricalTeamComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
  });

  it('renders team franchise title, rings banner, and trophy cabinet', () => {
    fixture.detectChanges();
    const req = http.expectOne('api/historical/team/relentless');
    req.flush(mockTeam);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Relentless');
    expect(el.textContent).toContain('2 Championship Rings');
    expect(el.querySelector('.championships-section')).not.toBeNull();
    expect(el.textContent).toContain('Summer League 2020');
  });

  it('renders all-time franchise leaders and season appearances', () => {
    fixture.detectChanges();
    http.expectOne('api/historical/team/relentless').flush(mockTeam);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.franchise-table')).not.toBeNull();
    expect(el.textContent).toContain('Veritas');
    expect(el.textContent).toContain('75'); // Goals
    expect(el.textContent).toContain('🏆 2'); // Rings
  });
});
