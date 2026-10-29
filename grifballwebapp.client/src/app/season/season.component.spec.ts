import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { RouterTestingHarness } from '@angular/router/testing';
import { By } from '@angular/platform-browser';
import { SeasonComponent } from './season.component';
import { ApiClientService } from '../api/apiClient.service';
import { ScheduleListComponent } from './scheduleList/scheduleList.component';
import { PlayoffBracketComponent } from './playoffBracket/playoffBracket.component';
import { TeamStandingsComponent } from './teamStandings/teamStandings.component';
import { SeasonThisWeekComponent } from './seasonThisWeek/seasonThisWeek.component';
import { input } from '@angular/core';
import { SeasonEvent } from '../shared/seasonEvents';

@Component({ selector: 'app-schedule-list', template: '' })
class StubScheduleList {}
@Component({ selector: 'app-playoff-bracket', template: '' })
class StubPlayoffBracket {}
@Component({ selector: 'app-team-standings', template: '' })
class StubTeamStandings { readonly loaded = signal(false); }
@Component({ selector: 'app-season-this-week', template: '' })
class StubThisWeek { readonly seasonID = input<number>(); }

describe('SeasonComponent', () => {
  let fixture: ComponentFixture<SeasonComponent>;
  let api: jasmine.SpyObj<ApiClientService>;
  let seasonID: string | null;

  beforeEach(async () => {
    seasonID = '5';
    api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID', 'getSeasonName', 'getCurrentAndFutureEvents']);
    api.getSeasonName.and.callFake((id: number) => of(`Season ${id}`));
    api.getCurrentSeasonID.and.returnValue(of(8));
    api.getCurrentAndFutureEvents.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [SeasonComponent],
      providers: [
        provideRouter([]),
        { provide: ApiClientService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => seasonID } } } },
      ]
    })
      .overrideComponent(SeasonComponent, {
        remove: { imports: [ScheduleListComponent, PlayoffBracketComponent, TeamStandingsComponent, SeasonThisWeekComponent] },
        add: { imports: [StubScheduleList, StubPlayoffBracket, StubTeamStandings, StubThisWeek] }
      })
      .compileComponents();

    fixture = TestBed.createComponent(SeasonComponent);
  });

  const heading = () => (fixture.nativeElement as HTMLElement).querySelector('h1')!.textContent;

  it('shows the name of the season in the route', () => {
    fixture.detectChanges();

    expect(api.getSeasonName).toHaveBeenCalledWith(5);
    expect(api.getCurrentSeasonID).not.toHaveBeenCalled();
    expect(heading()).toBe('Season 5');
  });

  it('falls back to the current season when the route has none', () => {
    seasonID = null;
    fixture.detectChanges();

    expect(api.getCurrentSeasonID).toHaveBeenCalled();
    expect(api.getSeasonName).toHaveBeenCalledWith(8);
    expect(heading()).toBe('Season 8');
  });


  it('shows the season name once it arrives, without a manual detectChanges', async () => {
    const name = new Subject<string>();
    api.getSeasonName.and.returnValue(name);
    await fixture.whenStable();

    name.next('Late Season');
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
    const name = new Subject<string>();
    api.getSeasonName.and.returnValue(name);
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
      api.getCurrentAndFutureEvents.and.returnValue(of([signup(-day, day)]));
      fixture.detectChanges();
      expect(signUpLink()).toBeDefined();
    });

    it('is not offered before signups open', () => {
      api.getCurrentAndFutureEvents.and.returnValue(of([signup(day, 2 * day)]));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });

    it('is not offered once signups have closed', () => {
      api.getCurrentAndFutureEvents.and.returnValue(of([signup(-2 * day, -day)]));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });

    it('is not offered without a signup window: none listed, another season\'s, or the events failing', () => {
      api.getCurrentAndFutureEvents.and.returnValue(of([signup(-day, day, 6), { ...signup(-day, day), eventType: 'Season' }]));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });

    it('is not offered when the events cannot be loaded, and the page still shows', () => {
      api.getCurrentAndFutureEvents.and.returnValue(throwError(() => new Error('down')));
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
      expect((fixture.nativeElement as HTMLElement).querySelector('.hud-page')!.classList).not.toContain('hud-waiting');
    });

    it('keeps the page hidden until it is known whether to offer it', () => {
      const events = new Subject<SeasonEvent[]>();
      api.getCurrentAndFutureEvents.and.returnValue(events);
      fixture.detectChanges();
      const page = () => (fixture.nativeElement as HTMLElement).querySelector('.hud-page')!;
      expect(page().classList).toContain('hud-waiting');
      events.next([signup(-day, day)]);
      fixture.detectChanges();
      expect(page().classList).not.toContain('hud-waiting');
      expect(signUpLink()).toBeDefined();
    });

    it('follows the clock: closing while the page is open takes it away', () => {
      api.getCurrentAndFutureEvents.and.returnValue(of([signup(-day, day)]));
      fixture.detectChanges();
      fixture.componentInstance.now.set(Date.now() + 2 * day);
      fixture.detectChanges();
      expect(signUpLink()).toBeUndefined();
    });
  });

  it('falls back to the overview for an unknown tab', () => {
    fixture.componentRef.setInput('tab', 'nope');
    fixture.detectChanges();
    expect(fixture.componentInstance.view()).toBe('overview');
  });
});

describe('SeasonComponent links', () => {
  it('links to the sub-pages of the current season', async () => {
    const api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID', 'getSeasonName', 'getCurrentAndFutureEvents']);
    api.getSeasonName.and.returnValue(of('Season 5'));
    const day = 24 * 3600 * 1000;
    api.getCurrentAndFutureEvents.and.returnValue(of([{ seasonID: 5, name: 'Season 5', eventType: 'Signup',
      start: new Date(Date.now() - day).toISOString(), end: new Date(Date.now() + day).toISOString() }]));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'season/:seasonID', component: SeasonComponent }]),
        { provide: ApiClientService, useValue: api },
      ]
    }).overrideComponent(SeasonComponent, {
      remove: { imports: [ScheduleListComponent, PlayoffBracketComponent, TeamStandingsComponent, SeasonThisWeekComponent] },
      add: { imports: [StubScheduleList, StubPlayoffBracket, StubTeamStandings, StubThisWeek] }
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/season/5', SeasonComponent);

    const hrefs = Array.from(harness.routeNativeElement!.querySelectorAll('a')).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(['/season/5/signupForm', '/season/5', '/season/5?tab=schedule', '/season/5?tab=bracket',
      '/season/5/teams', '/season/5/playergrades', '/season/5/signups']);
  });
});
