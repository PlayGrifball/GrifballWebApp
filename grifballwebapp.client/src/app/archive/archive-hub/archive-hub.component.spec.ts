import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ArchiveHubComponent, HistoricalCatalogItemDto, HistoricalLeaderboardsDto } from './archive-hub.component';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

describe('ArchiveHubComponent', () => {
  let component: ArchiveHubComponent;
  let fixture: ComponentFixture<ArchiveHubComponent>;
  let httpTesting: HttpTestingController;

  const mockCatalog: HistoricalCatalogItemDto[] = [
    {
      seasonID: 101,
      seasonName: 'Summer League 2011',
      year: 2011,
      leagueID: 1,
      leagueName: 'AGLA',
      leagueSlug: 'agla',
      gameVersionID: 1,
      gameVersionName: 'Halo 3',
      gameVersionSlug: 'halo3',
      teamCount: 12,
      playerCount: 60,
      champions: [
        {
          tier: 'Pro',
          teamName: 'Relentless',
          teamSlug: 'relentless',
          roster: [{ userID: 1, gamertag: 'Legendary', isCaptain: true }]
        }
      ]
    },
    {
      seasonID: 102,
      seasonName: 'Fall League 2014',
      year: 2014,
      leagueID: 1,
      leagueName: 'AGLA',
      leagueSlug: 'agla',
      gameVersionID: 2,
      gameVersionName: 'Halo 4',
      gameVersionSlug: 'halo4',
      teamCount: 8,
      playerCount: 40,
      champions: []
    }
  ];

  const mockLeaderboards: HistoricalLeaderboardsDto = {
    ringLeaders: [
      { userID: 1, gamertag: 'Legendary', ringsCount: 5, titles: ['Summer League 2011 (Pro)'] }
    ],
    franchiseDynasties: [
      { teamName: 'Relentless', teamSlug: 'relentless', championshipsCount: 4, seasonsActive: 10, titles: ['Summer League 2011 (Pro)'] }
    ],
    goalLeaders: [
      { userID: 1, gamertag: 'Legendary', statValue: 120, gamesPlayed: 30, perGame: 4.0, seasonsCount: 5 }
    ],
    killLeaders: [
      { userID: 1, gamertag: 'Legendary', statValue: 500, gamesPlayed: 30, perGame: 16.67, seasonsCount: 5, secondaryStat: '2.5 K/D' }
    ],
    gamesPlayedLeaders: [
      { userID: 1, gamertag: 'Legendary', statValue: 30, gamesPlayed: 30, seasonsCount: 5 }
    ],
    carrierStopLeaders: [
      { userID: 1, gamertag: 'Legendary', statValue: 45, gamesPlayed: 30, perGame: 1.5, seasonsCount: 5 }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ArchiveHubComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ArchiveHubComponent);
    component = fixture.componentInstance;
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should create and load catalog', () => {
    fixture.detectChanges();

    const req = httpTesting.expectOne('api/historical/catalog');
    expect(req.request.method).toBe('GET');
    req.flush(mockCatalog);

    expect(component.catalog().length).toBe(2);
    expect(component.availableGames()).toContain('Halo 3');
    expect(component.availableGames()).toContain('Halo 4');
  });

  it('should filter catalog by game version and search query', () => {
    fixture.detectChanges();
    const req = httpTesting.expectOne('api/historical/catalog');
    req.flush(mockCatalog);

    // Filter by game
    component.setGame('Halo 3');
    expect(component.filteredCatalog().length).toBe(1);
    expect(component.filteredCatalog()[0].seasonName).toBe('Summer League 2011');

    // Reset game and search query
    component.setGame('all');
    component.searchQuery.set('Fall');
    expect(component.filteredCatalog().length).toBe(1);
    expect(component.filteredCatalog()[0].seasonName).toBe('Fall League 2014');
  });

  it('should load leaderboards when switching to leaderboards tab', () => {
    fixture.detectChanges();
    const catReq = httpTesting.expectOne('api/historical/catalog');
    catReq.flush(mockCatalog);

    component.setTab('leaderboards');
    const lbReq = httpTesting.expectOne('api/historical/leaderboards');
    expect(lbReq.request.method).toBe('GET');
    lbReq.flush(mockLeaderboards);

    expect(component.leaderboards()).not.toBeNull();
    expect(component.leaderboards()!.ringLeaders.length).toBe(1);
    expect(component.leaderboards()!.ringLeaders[0].gamertag).toBe('Legendary');
  });
});
