import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeasonTeamsComponent } from './seasonTeams.component';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { ApiClientService } from '../../api/apiClient.service';
import { AccountService } from '../../account.service';
import { SeasonSummaryService } from '../seasonSummary';
import { of, throwError } from 'rxjs';
import { signal, WritableSignal } from '@angular/core';
import type { SeasonSummaryDto } from '../../api/dtos/seasonSummaryDto';
import type { SeasonTeamDto } from '../../api/dtos/seasonTeamDto';

describe('SeasonTeamsComponent', () => {
  let fixture: ComponentFixture<SeasonTeamsComponent>;
  let component: SeasonTeamsComponent;
  let api: jasmine.SpyObj<ApiClientService>;
  let summaries: jasmine.SpyObj<SeasonSummaryService>;
  let isEventOrganizer: WritableSignal<boolean>;
  let personID: WritableSignal<number | null>;

  const mockSummary: SeasonSummaryDto = {
    seasonID: 7,
    seasonName: 'Autumn Classic',
    isHistorical: false,
    historicalSourceUrl: null,
    signupCount: 20,
    teamCount: 2,
    matchCount: 10,
    bracketMatchCount: 4,
    playerStatCount: 30,
    champions: [],
    start: '2026-09-01T00:00:00Z',
    end: '2026-11-01T00:00:00Z',
    leagueID: 1,
    leagueName: 'GBL',
    leagueSlug: 'gbl',
    year: 2026,
    hasEnded: false,
    gameVersionID: 1,
    gameVersionName: 'Halo Infinite',
    format: 'LeagueSeason',
  };

  const mockTeams: SeasonTeamDto[] = [
    {
      teamID: 101,
      teamName: 'Orange Crush',
      wins: 8,
      losses: 2,
      tbd: 0,
      recordFromSource: false,
      titles: ['Pro'],
      draftCaptainOrder: 1,
      players: [
        { teamPlayerID: 1, userID: 10, gamertag: 'MasterChief', isCaptain: true, region: 'NA-East' },
        { teamPlayerID: 2, userID: 20, gamertag: 'Cortana', isCaptain: false, draftRound: 1, region: 'NA-West' },
      ],
    },
    {
      teamID: 102,
      teamName: 'Blue Bombers',
      wins: 5,
      losses: 5,
      tbd: 2,
      recordFromSource: false,
      titles: [],
      draftCaptainOrder: 2,
      players: [
        { teamPlayerID: 3, userID: 30, gamertag: 'Arbiter', isCaptain: true, region: 'EU' },
      ],
    },
  ];

  beforeEach(async () => {
    isEventOrganizer = signal(false);
    personID = signal<number | null>(null);

    api = jasmine.createSpyObj<ApiClientService>('ApiClientService', ['getSeasonTeams']);
    summaries = jasmine.createSpyObj<SeasonSummaryService>('SeasonSummaryService', ['get']);

    api.getSeasonTeams.and.returnValue(of(mockTeams));
    summaries.get.and.returnValue(of(mockSummary));

    await TestBed.configureTestingModule({
      imports: [SeasonTeamsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(new Map([['seasonID', '7']])),
          },
        },
        { provide: ApiClientService, useValue: api },
        { provide: SeasonSummaryService, useValue: summaries },
        {
          provide: AccountService,
          useValue: {
            isEventOrganizer,
            personID,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SeasonTeamsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads and renders the teams and their rosters', () => {
    expect(component.teams().length).toBe(2);
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Orange Crush');
    expect(text).toContain('Blue Bombers');
    expect(text).toContain('MasterChief');
    expect(text).toContain('Cortana');
    expect(text).toContain('Arbiter');
    expect(text).toContain('🏆 Pro Champions');
  });

  it('filters teams by team name or player gamertag in search input', () => {
    component.searchFilter.set('Cortana');
    fixture.detectChanges();

    expect(component.filteredTeams().length).toBe(1);
    expect(component.filteredTeams()[0].teamName).toBe('Orange Crush');

    component.searchFilter.set('Blue');
    fixture.detectChanges();

    expect(component.filteredTeams().length).toBe(1);
    expect(component.filteredTeams()[0].teamName).toBe('Blue Bombers');

    component.clearSearch();
    fixture.detectChanges();

    expect(component.filteredTeams().length).toBe(2);
  });

  it('displays empty state when search matches nothing', () => {
    component.searchFilter.set('NonExistentGamertag');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('No teams or rostered players match "NonExistentGamertag"');
  });

  it('renders historical banner when season is marked historical', () => {
    summaries.get.and.returnValue(of({
      ...mockSummary,
      isHistorical: true,
      historicalSourceUrl: 'https://docs.google.com/spreadsheets/d/test',
    }));

    component.loadData(7);
    fixture.detectChanges();

    expect(component.isHistorical()).toBeTrue();
    const banner = fixture.nativeElement.querySelector('app-backfilled-banner');
    expect(banner).not.toBeNull();
  });

  it('shows Open Draft Board button for event organizers or captains', () => {
    isEventOrganizer.set(true);
    fixture.detectChanges();

    const draftBtn = fixture.nativeElement.querySelector('.draft-board-link');
    expect(draftBtn).not.toBeNull();
    expect(draftBtn.getAttribute('href')).toContain('/season/7/draft');
  });

  it('handles error state when team loading fails', () => {
    api.getSeasonTeams.and.returnValue(throwError(() => ({ status: 404 })));
    component.loadData(999);
    fixture.detectChanges();

    expect(component.error()).toBe('Season not found');
    expect(fixture.nativeElement.textContent).toContain('Season not found');
  });
});
