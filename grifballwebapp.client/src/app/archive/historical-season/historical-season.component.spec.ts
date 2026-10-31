import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { HistoricalSeasonComponent, HistoricalSeasonDto } from './historical-season.component';

describe('HistoricalSeasonComponent', () => {
  let fixture: ComponentFixture<HistoricalSeasonComponent>;
  let component: HistoricalSeasonComponent;
  let http: HttpTestingController;

  const mockSeason: HistoricalSeasonDto = {
    seasonID: 15,
    seasonName: 'Summer League 2020 (Halo 3)',
    year: 2020,
    leagueID: 1,
    leagueName: 'AGLA',
    leagueSlug: 'agla',
    gameVersionID: 1,
    gameVersionName: 'Halo 3',
    gameVersionSlug: 'halo3',
    division: 'Pro',
    historicalSourceUrl: 'https://docs.google.com/test',
    champions: [
      {
        tier: 'Pro',
        teamName: 'Relentless',
        teamSlug: 'relentless',
        roster: [
          { userID: 101, gamertag: 'Veritas', isCaptain: true },
          { userID: 102, gamertag: 'SpartanTwo', isCaptain: false }
        ]
      }
    ],
    teams: [
      {
        teamName: 'Relentless',
        teamSlug: 'relentless',
        gamesPlayed: 14,
        wins: 12,
        losses: 2,
        winPercent: 85.7,
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
            kdRatio: 2.0,
            carrierKills: 40
          }
        ]
      }
    ],
    playerStats: [
      {
        playerSeasonStatID: 1,
        userID: 101,
        gamertag: 'Veritas',
        teamName: 'Relentless',
        teamSlug: 'relentless',
        division: 'Pro',
        stintIndex: 1,
        isPlayoffs: false,
        isCombined: false,
        gamesPlayed: 14,
        wins: 12,
        losses: 2,
        winPercent: 85.7,
        goals: 28,
        kills: 180,
        deaths: 90,
        assists: 30,
        kdRatio: 2.0,
        kdSpread: 90,
        carrierKills: 40,
        sprees: 10,
        multiKills: 6,
        exterminations: 1
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HistoricalSeasonComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '15' } } } }
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HistoricalSeasonComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
  });

  it('loads and renders the historical season header and champion presentation', () => {
    fixture.detectChanges();
    const req = http.expectOne('api/historical/season/15');
    req.flush(mockSeason);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Summer League 2020 (Halo 3)');
    expect(el.textContent).toContain('2020');
    expect(el.textContent).toContain('AGLA');
    expect(el.textContent).toContain('Halo 3');

    // Champions banner
    expect(el.querySelector('.champions-section')).not.toBeNull();
    expect(el.textContent).toContain('Relentless');
    expect(el.textContent).toContain('Veritas');
    expect(el.textContent).toContain('C'); // Captain badge
  });

  it('toggles between teams tab and player stats tab', () => {
    fixture.detectChanges();
    http.expectOne('api/historical/season/15').flush(mockSeason);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.teams-grid')).not.toBeNull();
    expect(el.querySelector('.season-table')).toBeNull();

    const tabs = el.querySelectorAll<HTMLButtonElement>('.hud-tab');
    expect(tabs.length).toBe(2);

    // Switch to stats tab
    tabs[1].click();
    fixture.detectChanges();

    expect(el.querySelector('.teams-grid')).toBeNull();
    expect(el.querySelector('.season-table')).not.toBeNull();
    expect(el.textContent).toContain('Veritas');
    expect(el.textContent).toContain('28'); // Goals
  });
});
