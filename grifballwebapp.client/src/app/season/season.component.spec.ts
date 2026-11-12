import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { RouterTestingHarness } from '@angular/router/testing';
import { By } from '@angular/platform-browser';
import { SeasonComponent } from './season.component';
import { HomeService, SeasonService } from '@api/services';
import { ScheduleListComponent } from './scheduleList/scheduleList.component';
import { PlayoffBracketComponent } from './playoffBracket/playoffBracket.component';
import { TeamStandingsComponent } from './teamStandings/teamStandings.component';
import { SeasonThisWeekComponent } from './seasonThisWeek/seasonThisWeek.component';
import { input, output } from '@angular/core';
import { SeasonEvent } from '../shared/seasonEvents';
import { SeasonSummaryService } from './seasonSummary';
import type { SeasonChampionDto, SeasonSummaryDto } from '@api/models';
import { seasonInfo } from '../api/seasonInfo.stub';
import { SeasonPlayerStatsComponent } from './seasonPlayerStats/seasonPlayerStats.component';
import { VodListComponent } from '../shared/vodList/vodList.component';
import { StubVodListComponent } from '../shared/vodList/vodList.stub';

/** A season as GetSeasonSummary answers: one played in the app unless changed. */
function seasonSummary(id: number, changes: Partial<SeasonSummaryDto> = {}): SeasonSummaryDto {
  return {
    ...seasonInfo(id, { hasEnded: false }), teamCount: 4, signupCount: 10, matchCount: 6, bracketMatchCount: 3, playerStatCount: 0, champions: [],
    ...changes,
  };
}

@Component({ selector: 'app-schedule-list', template: '' })
class StubScheduleList { readonly seasonID = input<number>(); }
@Component({ selector: 'app-playoff-bracket', template: '' })
class StubPlayoffBracket {
  readonly seasonID = input<number>();
  readonly champions = input<SeasonChampionDto[] | undefined>();
  readonly hasEnded = input(false);
  readonly bracketChanged = output<void>();
}
@Component({ selector: 'app-team-standings', template: '' })
class StubTeamStandings { readonly seasonID = input<number>(); readonly loaded = signal(false); }
@Component({ selector: 'app-season-this-week', template: '' })
class StubThisWeek { readonly seasonID = input<number>(); }
@Component({ selector: 'app-season-player-stats', template: '' })
class StubPlayerStats { readonly seasonID = input<number>(); }

describe('SeasonComponent', () => {
  let fixture: ComponentFixture<SeasonComponent>;
  let api: jasmine.SpyObj<SeasonService>;
  let home: jasmine.SpyObj<HomeService>;
  let summaries: jasmine.SpyObj<SeasonSummaryService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    home = jasmine.createSpyObj<HomeService>('HomeService', ['currentAndFutureEvents']);
    summaries = jasmine.createSpyObj('SeasonSummaryService', ['get']);
    summaries.get.and.callFake((id: number) => of(seasonSummary(id)));
    api.getCurrentSeasonId.and.returnValue(of(8));
    home.currentAndFutureEvents.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [SeasonComponent],
      providers: [
        provideRouter([]),
        { provide: SeasonService, useValue: api },
        { provide: HomeService, useValue: home },
        { provide: SeasonSummaryService, useValue: summaries },
      ]
    })
      .overrideComponent(SeasonComponent, {
        remove: { imports: [ScheduleListComponent, PlayoffBracketComponent, TeamStandingsComponent, SeasonThisWeekComponent, SeasonPlayerStatsComponent, VodListComponent] },
        add: { imports: [StubScheduleList, StubPlayoffBracket, StubTeamStandings, StubThisWeek, StubPlayerStats, StubVodListComponent] }
      })
      .compileComponents();

    fixture = TestBed.createComponent(SeasonComponent);
    // As the router gives it: the :seasonID of the route, as text
    fixture.componentRef.setInput('seasonID', '5');
  });

  const heading = () => (fixture.nativeElement as HTMLElement).querySelector('h1')!.textContent;

  it('shows the name of the season in the route', () => {
    fixture.detectChanges();

    expect(summaries.get).toHaveBeenCalledWith(5);
    expect(api.getCurrentSeasonId).not.toHaveBeenCalled();
    expect(heading()).toBe('Season 5');
  });

  it('lists the season\'s VODs on its overview', () => {
    fixture.detectChanges();

    const vods = fixture.debugElement.query(By.directive(StubVodListComponent)).componentInstance as StubVodListComponent;
    expect(vods.scope()).toBe('season');
    expect(vods.id()).toBe(5);
  });

  it('falls back to the current season when the route has none', () => {
    fixture.componentRef.setInput('seasonID', '0');
    fixture.detectChanges();

    expect(api.getCurrentSeasonId).toHaveBeenCalled();
    expect(summaries.get).toHaveBeenCalledWith(8);
    expect(heading()).toBe('Season 8');
  });


  it('loads the new season when the route moves to another, and gives it to its sections', () => {
    fixture.detectChanges();
    expect(heading()).toBe('Season 5');

    fixture.componentRef.setInput('seasonID', '12');
    fixture.detectChanges();

    expect(summaries.get).toHaveBeenCalledWith(12);
    expect(heading()).toBe('Season 12');
    const standings = fixture.debugElement.query(By.directive(StubTeamStandings)).componentInstance as StubTeamStandings;
    expect(standings.seasonID()).toBe(12);
    const thisWeek = fixture.debugElement.query(By.directive(StubThisWeek)).componentInstance as StubThisWeek;
    expect(thisWeek.seasonID()).toBe(12);
    const vods = fixture.debugElement.query(By.directive(StubVodListComponent)).componentInstance as StubVodListComponent;
    expect(vods.id()).toBe(12);
  });

  it('gives the schedule and bracket the season too', () => {
    fixture.componentRef.setInput('tab', 'schedule');
    fixture.detectChanges();
    expect((fixture.debugElement.query(By.directive(StubScheduleList)).componentInstance as StubScheduleList).seasonID()).toBe(5);
    fixture.componentRef.setInput('tab', 'bracket');
    fixture.componentRef.setInput('seasonID', '12');
    fixture.detectChanges();
    expect((fixture.debugElement.query(By.directive(StubPlayoffBracket)).componentInstance as StubPlayoffBracket).seasonID()).toBe(12);
  });

  it('hides the previous season while the new one loads, and drops its late summary', () => {
    const answers = new Map<number, Subject<SeasonSummaryDto>>();
    summaries.get.and.callFake((id: number) => {
      const answer = new Subject<SeasonSummaryDto>();
      answers.set(id, answer);
      return answer;
    });
    fixture.detectChanges();

    fixture.componentRef.setInput('seasonID', '12');
    fixture.detectChanges();
    expect(fixture.componentInstance.summary()).toBeNull();
    expect(fixture.componentInstance.nameLoaded()).toBeFalse();

    answers.get(12)!.next(seasonSummary(12));
    answers.get(5)!.next(seasonSummary(5));
    answers.get(5)!.error(new Error('late'));
    fixture.detectChanges();

    expect(heading()).toBe('Season 12');
    expect(fixture.componentInstance.summary()!.seasonID).toBe(12);
  });

  it('drops the current season\'s ID when the route moves on before it arrives', () => {
    const current = new Subject<number>();
    api.getCurrentSeasonId.and.returnValue(current);
    fixture.componentRef.setInput('seasonID', '0');
    fixture.detectChanges();

    fixture.componentRef.setInput('seasonID', '12');
    fixture.detectChanges();
    current.next(8);
    fixture.detectChanges();

    expect(summaries.get).not.toHaveBeenCalledWith(8);
    expect(fixture.componentInstance.shownSeasonID()).toBe(12);
    expect(heading()).toBe('Season 12');
  });

  it('shows the season name once it arrives, without a manual detectChanges', async () => {
    const name = new Subject<SeasonSummaryDto>();
    summaries.get.and.returnValue(name);
    await fixture.whenStable();

    name.next(seasonSummary(5, { seasonName: 'Late Season' }));
    await fixture.whenStable();

    expect(heading()).toBe('Late Season');
  });

  const shown = () => ['app-team-standings', 'app-season-this-week', 'app-schedule-list', 'app-playoff-bracket']
    .filter(tag => (fixture.nativeElement as HTMLElement).querySelector(tag) !== null);

  it('shows the standings and this week by default', () => {
    fixture.detectChanges();
    expect(fixture.componentInstance.view()).toBe('overview');
    expect(shown()).toEqual(['app-team-standings', 'app-season-this-week']);
  });

  for (const [tab, tag, kicker] of [['schedule', 'app-schedule-list', 'Regular season'], ['bracket', 'app-playoff-bracket', 'Playoffs']]) {
    it(`shows only the ${tab} for ?tab=${tab}`, () => {
      fixture.componentRef.setInput('tab', tab);
      fixture.detectChanges();
      expect(shown()).toEqual([tag]);
      expect(fixture.componentInstance.kicker()).toBe(kicker);
      expect((fixture.nativeElement as HTMLElement).querySelector('[aria-current="page"]')!.textContent!.trim().toLowerCase()).toBe(tab);
    });
  }

  it('gives the bracket tab\'s bracket the season\'s champions for its champion slot', () => {
    const champions: SeasonChampionDto[] = [{ tier: 'Open', teamID: 41, teamName: 'Relentless', roster: [] }];
    summaries.get.and.callFake((id: number) => of(seasonSummary(id, { champions })));
    fixture.componentRef.setInput('tab', 'bracket');
    fixture.detectChanges();

    const bracket = fixture.debugElement.query(By.directive(StubPlayoffBracket)).componentInstance as StubPlayoffBracket;
    expect(bracket.champions()).toEqual(champions);
    expect(bracket.hasEnded()).toBeFalse();
  });

  it('tells the bracket tab\'s bracket that the season has ended', () => {
    summaries.get.and.callFake((id: number) => of(seasonSummary(id, { hasEnded: true })));
    fixture.componentRef.setInput('tab', 'bracket');
    fixture.detectChanges();

    const bracket = fixture.debugElement.query(By.directive(StubPlayoffBracket)).componentInstance as StubPlayoffBracket;
    expect(bracket.hasEnded()).toBeTrue();
  });

  it('fetches the season\'s champions again once the bracket is created again or reseeded', () => {
    fixture.componentRef.setInput('tab', 'bracket');
    fixture.detectChanges();
    expect(summaries.get).toHaveBeenCalledTimes(1);
    const champions: SeasonChampionDto[] = [{ tier: 'Open', teamID: 41, teamName: 'Relentless', roster: [] }];
    summaries.get.and.callFake((id: number) => of(seasonSummary(id, { champions })));

    const bracket = fixture.debugElement.query(By.directive(StubPlayoffBracket)).componentInstance as StubPlayoffBracket;
    bracket.bracketChanged.emit();
    fixture.detectChanges();

    expect(summaries.get).toHaveBeenCalledTimes(2);
    expect(summaries.get).toHaveBeenCalledWith(5);
    expect(bracket.champions()).toEqual(champions);
  });

  it('shows this week once the standings above it are in', () => {
    fixture.detectChanges();
    const aside = () => (fixture.nativeElement as HTMLElement).querySelector('aside')!;
    expect(aside().classList).toContain('hud-waiting');
    const standings = fixture.debugElement.query(By.directive(StubTeamStandings)).componentInstance as StubTeamStandings;
    standings.loaded.set(true);
    fixture.detectChanges();
    expect(aside().classList).not.toContain('hud-waiting');
  });

  it('keeps the page hidden until the season name is in, also when it fails', () => {
    const name = new Subject<SeasonSummaryDto>();
    summaries.get.and.returnValue(name);
    fixture.detectChanges();
    const page = () => (fixture.nativeElement as HTMLElement).querySelector('.hud-page')!;
    expect(page().classList).toContain('hud-waiting');
    name.error(new Error('down'));
    fixture.detectChanges();
    expect(page().classList).not.toContain('hud-waiting');
  });

  describe('Sign up', () => {
    const day = 24 * 3600 * 1000;
    const iso = (offset: number) => new Date(Date.now() + offset).toISOString();
    const signup = (start: number, end: number, seasonID = 5): SeasonEvent =>
      ({ seasonID, name: 'Season 5', start: iso(start), end: iso(end), eventType: 'Signup' });
    const signUpLink = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.season-head a'))
      .find(a => a.textContent!.trim() === 'Sign up');

    it('is offered while the season\'s signups are open', () => {
      home.currentAndFutureEvents.and.returnValue(of([signup(-day, day)]));
      fixture.detectChanges();
      expect(signUpLink()).toBeDefined();
    });

    it('is not offered before signups open', () => {
      home.currentAndFutureEvents.and.returnValue(of([signup(day, 2 * day)]));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });

    it('is not offered once signups have closed', () => {
      home.currentAndFutureEvents.and.returnValue(of([signup(-2 * day, -day)]));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });

    it('is not offered without a signup window: none listed, another season\'s, or the events failing', () => {
      home.currentAndFutureEvents.and.returnValue(of([signup(-day, day, 6), { ...signup(-day, day), eventType: 'Season' }]));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });

    it('is not offered when the events cannot be loaded, and the page still shows', () => {
      home.currentAndFutureEvents.and.returnValue(throwError(() => new Error('down')));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
      expect((fixture.nativeElement as HTMLElement).querySelector('.hud-page')!.classList).not.toContain('hud-waiting');
    });

    it('keeps the page hidden until it is known whether to offer it', () => {
      const events = new Subject<SeasonEvent[]>();
      home.currentAndFutureEvents.and.returnValue(events);
      fixture.detectChanges();
      const page = () => (fixture.nativeElement as HTMLElement).querySelector('.hud-page')!;
      expect(page().classList).toContain('hud-waiting');
      events.next([signup(-day, day)]);
      fixture.detectChanges();
      expect(page().classList).not.toContain('hud-waiting');
      expect(signUpLink()).toBeDefined();
    });

    it('follows the clock: closing while the page is open takes it away', () => {
      home.currentAndFutureEvents.and.returnValue(of([signup(-day, day)]));
      fixture.detectChanges();
      fixture.componentInstance.now.set(Date.now() + 2 * day);
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });
  });

  describe('a season backfilled from historical records', () => {
    const url = 'https://docs.google.com/spreadsheets/d/x';
    const el = () => fixture.nativeElement as HTMLElement;
    const notices = () => Array.from(el().querySelectorAll('app-data-notice')).map(n => n.textContent!.trim());

    beforeEach(() => summaries.get.and.callFake((id: number) => of(seasonSummary(id, {
      seasonName: 'Summer League 2010', year: 2010, gameVersionName: 'Halo 3', division: 'Pro', isHistorical: true,
      historicalSourceUrl: url, signupCount: 0, matchCount: 0, bracketMatchCount: 0, playerStatCount: 12,
      champions: [{ tier: 'Pro', teamID: 40, teamName: 'Relentless', roster: [{ userID: 7, gamertag: 'Captain R', isCaptain: true }] }],
    }))));

    it('shows the banner with its spreadsheet, its game and division, champions and player stats', () => {
      fixture.detectChanges();
      expect(el().querySelector('app-backfilled-banner a')!.getAttribute('href')).toBe(url);
      expect(Array.from(el().querySelectorAll('.season-meta__pill')).map(p => p.textContent!.trim())).toEqual(['2010', 'AGLA', 'Halo 3', 'Pro']);
      expect(el().querySelector('app-season-champions')!.textContent).toContain('Relentless');
      expect(el().querySelector('app-season-player-stats')).not.toBeNull();
    });

    it('says the results were not recorded in place of this week', () => {
      fixture.detectChanges();
      expect(shown()).toEqual(['app-team-standings']);
      expect(notices()).toEqual(["Match results weren't recorded for this season."]);
    });

    for (const [tab, text] of [['schedule', 'match schedule'], ['bracket', 'playoff bracket']]) {
      it(`says the ${text} was not recorded on ?tab=${tab}, with the spreadsheet`, () => {
        fixture.componentRef.setInput('tab', tab);
        fixture.detectChanges();
        expect(shown()).toEqual([]);
        expect(notices()[0]).toContain(`The ${text} wasn't recorded`);
        expect(el().querySelector('app-data-notice a')!.getAttribute('href')).toBe(url);
      });
    }

    it('shows what it does have, like matches pulled from Waypoint, as usual', () => {
      summaries.get.and.callFake((id: number) => of(seasonSummary(id, { isHistorical: true, matchCount: 8, bracketMatchCount: 0 })));
      fixture.detectChanges();
      expect(shown()).toEqual(['app-team-standings', 'app-season-this-week']);
      expect(el().querySelector('app-season-player-stats')).toBeNull();
    });
  });

  describe('a season played in the app', () => {
    const el = () => fixture.nativeElement as HTMLElement;
    const notices = () => Array.from(el().querySelectorAll('app-data-notice')).map(n => n.textContent!.trim());

    it('says what was not recorded once it has ended without it, with no banner', () => {
      summaries.get.and.callFake((id: number) => of(seasonSummary(id, { hasEnded: true, matchCount: 0, bracketMatchCount: 0 })));
      fixture.detectChanges();
      expect(el().querySelector('app-backfilled-banner')).toBeNull();
      expect(notices()).toEqual(["Match results weren't recorded for this season."]);
      fixture.componentRef.setInput('tab', 'bracket');
      fixture.detectChanges();
      expect(notices()).toEqual(["The playoff bracket wasn't recorded for this season."]);
      expect(el().querySelector('app-data-notice a')).toBeNull();
    });

    it('shows an ended season what it has as usual', () => {
      summaries.get.and.callFake((id: number) => of(seasonSummary(id, { hasEnded: true, bracketMatchCount: 0 })));
      fixture.detectChanges();
      expect(shown()).toEqual(['app-team-standings', 'app-season-this-week']);
      fixture.componentRef.setInput('tab', 'schedule');
      fixture.detectChanges();
      expect(shown()).toEqual(['app-schedule-list']);
    });

    it('shows the empty sections of a season still going, as there is more to come', () => {
      summaries.get.and.callFake((id: number) => of(seasonSummary(id, { hasEnded: false, matchCount: 0, bracketMatchCount: 0 })));
      fixture.componentRef.setInput('tab', 'bracket');
      fixture.detectChanges();
      expect(notices()).toEqual([]);
      expect(shown()).toEqual(['app-playoff-bracket']);
    });
  });

  describe('formats', () => {
    const el = () => fixture.nativeElement as HTMLElement;
    const notices = () => Array.from(el().querySelectorAll('app-data-notice')).map(n => n.textContent!.trim());
    const pills = () => Array.from(el().querySelectorAll('.season-meta__pill')).map(p => p.textContent!.trim());
    const champions = [{ tier: 'Open', teamID: 1, teamName: 'Winners', roster: [] }];

    it('shows a tournament its bracket in place of the standings and this week, and no schedule', () => {
      summaries.get.and.callFake((id: number) => of(seasonSummary(id, { format: 'Tournament', champions })));
      fixture.detectChanges();
      expect(pills()).toContain('Tournament');
      expect(notices()).toEqual(['A tournament: a bracket only, with no regular season schedule or standings.']);
      expect(el().querySelector('app-playoff-bracket')).not.toBeNull();
      expect(el().querySelector('app-team-standings, app-season-this-week')).toBeNull();
      expect(el().querySelector('app-season-champions')).not.toBeNull();

      fixture.componentRef.setInput('tab', 'schedule');
      fixture.detectChanges();
      expect(el().querySelector('app-schedule-list')).toBeNull();
      expect(notices()).toContain('A tournament has no regular season schedule: its games are in the bracket.');
    });

    it('says a tournament\'s bracket was not recorded when it has none', () => {
      summaries.get.and.callFake((id: number) => of(seasonSummary(id, { format: 'Tournament', bracketMatchCount: 0, isHistorical: true })));
      fixture.detectChanges();
      expect(notices()).toContain('The bracket wasn\'t recorded for this season.');
      expect(el().querySelector('app-playoff-bracket')).toBeNull();
    });

    it('shows a league season no format pill or notice', () => {
      summaries.get.and.callFake((id: number) => of(seasonSummary(id, { format: 'LeagueSeason' })));
      fixture.detectChanges();
      expect(pills()).not.toContain('League season');
      expect(notices()).toEqual([]);
    });
  });

  it('shows no banner or notices for a season played in the app', () => {
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-backfilled-banner, app-data-notice, app-season-champions')).toBeNull();
  });

  it('falls back to the overview for an unknown tab', () => {
    fixture.componentRef.setInput('tab', 'nope');
    fixture.detectChanges();
    expect(fixture.componentInstance.view()).toBe('overview');
  });
});

describe('SeasonComponent links', () => {
  it('links to the sub-pages of the current season', async () => {
    const api = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    const home = jasmine.createSpyObj<HomeService>('HomeService', ['currentAndFutureEvents']);
    const summaries = jasmine.createSpyObj('SeasonSummaryService', ['get']);
    summaries.get.and.returnValue(of(seasonSummary(5)));
    const day = 24 * 3600 * 1000;
    home.currentAndFutureEvents.and.returnValue(of([{ seasonID: 5, name: 'Season 5', eventType: 'Signup',
      start: new Date(Date.now() - day).toISOString(), end: new Date(Date.now() + day).toISOString() }]));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'season/:seasonID', component: SeasonComponent }], withComponentInputBinding()),
        { provide: SeasonService, useValue: api },
        { provide: HomeService, useValue: home },
        { provide: SeasonSummaryService, useValue: summaries },
      ]
    }).overrideComponent(SeasonComponent, {
      remove: { imports: [ScheduleListComponent, PlayoffBracketComponent, TeamStandingsComponent, SeasonThisWeekComponent, VodListComponent] },
      add: { imports: [StubScheduleList, StubPlayoffBracket, StubTeamStandings, StubThisWeek, StubVodListComponent] }
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/season/5', SeasonComponent);

    const hrefs = Array.from(harness.routeNativeElement!.querySelectorAll('a')).map(a => a.getAttribute('href'));
    // The league and game labels first: each opens the season list filtered by it
    expect(hrefs).toEqual(['/seasons?league=1', '/seasons?game=6', '/season/5/signupForm', '/season/5', '/season/5?tab=schedule', '/season/5?tab=bracket',
      '/season/5/teams', '/season/5/playergrades', '/season/5/signups']);
  });

  it('shows the other season when the router goes from one season to another (the page is kept)', async () => {
    const api = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    const home = jasmine.createSpyObj<HomeService>('HomeService', ['currentAndFutureEvents']);
    const summaries = jasmine.createSpyObj('SeasonSummaryService', ['get']);
    summaries.get.and.callFake((id: number) => of(seasonSummary(id)));
    home.currentAndFutureEvents.and.returnValue(of([]));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'season/:seasonID', component: SeasonComponent }], withComponentInputBinding()),
        { provide: SeasonService, useValue: api },
        { provide: HomeService, useValue: home },
        { provide: SeasonSummaryService, useValue: summaries },
      ]
    }).overrideComponent(SeasonComponent, {
      remove: { imports: [ScheduleListComponent, PlayoffBracketComponent, TeamStandingsComponent, SeasonThisWeekComponent, VodListComponent] },
      add: { imports: [StubScheduleList, StubPlayoffBracket, StubTeamStandings, StubThisWeek, StubVodListComponent] }
    });
    const harness = await RouterTestingHarness.create();
    const first = await harness.navigateByUrl('/season/5', SeasonComponent);
    const second = await harness.navigateByUrl('/season/12', SeasonComponent);

    expect(second).toBe(first);
    expect(summaries.get).toHaveBeenCalledWith(12);
    expect(harness.routeNativeElement!.querySelector('h1')!.textContent).toBe('Season 12');
  });
});
