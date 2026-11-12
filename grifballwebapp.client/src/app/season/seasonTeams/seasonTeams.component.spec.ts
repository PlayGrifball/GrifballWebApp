import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeasonTeamsComponent } from './seasonTeams.component';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { SeasonService } from '@api/services';
import { AccountService } from '../../account.service';
import { SeasonSummaryService } from '../seasonSummary';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';
import { signal, WritableSignal } from '@angular/core';
import { SeasonSummaryDto } from '@api/models';
import { SeasonTeamDto } from '@api/models';

describe('SeasonTeamsComponent', () => {
  let fixture: ComponentFixture<SeasonTeamsComponent>;
  let component: SeasonTeamsComponent;
  let api: jasmine.SpyObj<SeasonService>;
  let summaries: jasmine.SpyObj<SeasonSummaryService>;
  let isEventOrganizer: WritableSignal<boolean>;
  let personID: WritableSignal<number | null>;
  let params: BehaviorSubject<ParamMap>;

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
    format: 'LeagueSeason', division: null, gameVersionSlug: null
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
        { teamPlayerID: 1, userID: 10, gamertag: 'MasterChief', isCaptain: true, region: 'NA-East', draftCaptainOrder: null, draftPick: null, draftRound: null },
        { teamPlayerID: 2, userID: 20, gamertag: 'Cortana', isCaptain: false, draftRound: 1, region: 'NA-West', draftCaptainOrder: null, draftPick: null },
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
        { teamPlayerID: 3, userID: 30, gamertag: 'Arbiter', isCaptain: true, region: 'EU', draftCaptainOrder: null, draftPick: null, draftRound: null },
      ],
    },
  ];

  beforeEach(async () => {
    isEventOrganizer = signal(false);
    personID = signal<number | null>(null);
    params = new BehaviorSubject(convertToParamMap({ seasonID: '7' }));

    api = jasmine.createSpyObj<SeasonService>('SeasonService', ['getSeasonTeams']);
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
            paramMap: params,
          },
        },
        { provide: SeasonService, useValue: api },
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
    expect(component.teams()).toHaveSize(2);
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

    expect(component.filteredTeams()).toHaveSize(1);
    expect(component.filteredTeams()[0].teamName).toBe('Orange Crush');

    component.searchFilter.set('Blue');
    fixture.detectChanges();

    expect(component.filteredTeams()).toHaveSize(1);
    expect(component.filteredTeams()[0].teamName).toBe('Blue Bombers');

    component.clearSearch();
    fixture.detectChanges();

    expect(component.filteredTeams()).toHaveSize(2);
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

  it('says the teams failed to load when the error is not a missing season', () => {
    api.getSeasonTeams.and.returnValue(throwError(() => ({ status: 500 })));
    component.loadData(7);
    fixture.detectChanges();

    expect(component.error()).toBe('Failed to load season teams');
    expect(component.loading()).toBeFalse();
  });

  it('ignores a route without a season', () => {
    api.getSeasonTeams.calls.reset();
    params.next(convertToParamMap({}));

    expect(api.getSeasonTeams).not.toHaveBeenCalled();
    expect(component.seasonID()).toBe(7);
  });

  it('shows only the season navigated to when the previous season answers late', () => {
    const season5Teams = new Subject<SeasonTeamDto[]>();
    const season5Summary = new Subject<SeasonSummaryDto>();
    const season12Teams = new Subject<SeasonTeamDto[]>();
    api.getSeasonTeams.and.callFake(p => p.seasonID === 5 ? season5Teams : season12Teams);
    summaries.get.and.callFake(id => id === 5 ? season5Summary : throwError(() => ({ status: 500 })));

    params.next(convertToParamMap({ seasonID: '5' }));
    params.next(convertToParamMap({ seasonID: '12' }));
    season12Teams.next([mockTeams[1]]);
    season5Teams.next(mockTeams);
    season5Summary.next({ ...mockSummary, seasonID: 5, seasonName: 'Season Five' });
    fixture.detectChanges();

    expect(component.seasonID()).toBe(12);
    expect(component.teams().map(t => t.teamID)).toEqual([102]);
    // Season 12's summary failed: nothing of season 5's summary is left
    expect(component.summary()).toBeNull();
    expect(component.canAccessDraft()).toBeFalse();
    expect(fixture.nativeElement.textContent).not.toContain('Season Five');
  });

  it('forgets the previous season\'s summary when the next one fails', () => {
    expect(component.summary()?.seasonName).toBe('Autumn Classic');

    summaries.get.and.returnValue(throwError(() => ({ status: 500 })));
    params.next(convertToParamMap({ seasonID: '12' }));
    fixture.detectChanges();

    expect(component.summary()).toBeNull();
    expect(component.isHistorical()).toBeFalse();
    expect(component.canAccessDraft()).toBeFalse();
    // The rosters still show without it
    expect(component.teams()).toHaveSize(2);
    expect(fixture.nativeElement.querySelector('.draft-board-link')).toBeNull();
  });

  describe('draft board shortcut', () => {
    beforeEach(() => {
      // A finished, backfilled season: only organizers and captains get the shortcut
      summaries.get.and.returnValue(of({ ...mockSummary, isHistorical: true }));
      component.loadData(7);
      fixture.detectChanges();
    });

    it('is hidden from a signed-out visitor', () => {
      expect(component.canAccessDraft()).toBeFalse();
      expect(fixture.nativeElement.querySelector('.draft-board-link')).toBeNull();
    });

    it('is hidden from a player who is not a captain', () => {
      personID.set(20);
      fixture.detectChanges();

      expect(component.isCaptainOfAnyTeam()).toBeFalse();
      expect(fixture.nativeElement.querySelector('.draft-board-link')).toBeNull();
    });

    it('is shown to a captain of one of the teams', () => {
      personID.set(30);
      fixture.detectChanges();

      expect(component.isCaptainOfAnyTeam()).toBeTrue();
      expect(fixture.nativeElement.querySelector('.draft-board-link').getAttribute('href')).toBe('/season/7/draft');
    });

    it('is shown to everyone while a current season has signups', () => {
      summaries.get.and.returnValue(of(mockSummary));
      component.loadData(7);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.draft-board-link').getAttribute('href')).toBe('/season/7/draft');
    });
  });

  it('links every player to their profile, with ? for a player without a name', () => {
    api.getSeasonTeams.and.returnValue(of([{
      ...mockTeams[1],
      players: [{ teamPlayerID: 9, userID: 90, gamertag: '', isCaptain: false, draftCaptainOrder: null, draftPick: null, draftRound: null, region: null }],
    }]));
    component.loadData(7);
    fixture.detectChanges();

    const player: HTMLElement = fixture.nativeElement.querySelector('.team-card__player');
    expect(player.querySelector('.hud-avatar')!.textContent!.trim()).toBe('?');
    expect(player.querySelector('a.player-name')!.getAttribute('href')).toBe('/profile/90');
  });
});
