import { DatePipe } from '@angular/common';
import { By } from '@angular/platform-browser';
import { NgForm } from '@angular/forms';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ScheduleListComponent } from './scheduleList.component';
import { conflictText, RECOMMEND_POLL_MS } from './scheduleList.store';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { EventEmitter, signal, WritableSignal } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { DateTime } from 'luxon';
import { AccountService } from '@core/auth/account.service';
import { CreateRegularMatchesDialogComponent } from './createRegularMatchesDialog/createRegularMatchesDialog.component';
import { Subject } from 'rxjs';
import type { OnDemandJobStatusDto, ScheduleRecommendationDto, ScheduleRecommendationPageDto } from '@api/models';

describe('ScheduleListComponent', () => {
  let component: ScheduleListComponent;
  let fixture: ComponentFixture<ScheduleListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScheduleListComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ScheduleListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

describe('ScheduleListComponent behaviour', () => {
  let fixture: ComponentFixture<ScheduleListComponent>;
  let component: ScheduleListComponent;
  let http: HttpTestingController;
  let dialog: jasmine.SpyObj<MatDialog>;
  let isEventOrganizer: WritableSignal<boolean>;

  const unscheduled = (id: number, complete = false) => ({
    seasonMatchID: id, homeCaptain: `Home${id}`, awayCaptain: `Away${id}`, homeTeam: `Home Team${id}`, awayTeam: `Away Team${id}`, complete
  });
  const scheduled = (id: number, iso: string, complete = false) => ({
    seasonMatchID: id, homeCaptain: `Home${id}`, awayCaptain: `Away${id}`, homeTeam: `Home Team${id}`, awayTeam: `Away Team${id}`, complete, time: iso
  });

  beforeEach(async () => {
    isEventOrganizer = signal(false);
    dialog = jasmine.createSpyObj('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [ScheduleListComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        provideLuxonDatetimeAdapter(),
        { provide: AccountService, useValue: { isEventOrganizer } },
      ]
    })
      .overrideProvider(MatDialog, { useValue: dialog })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ScheduleListComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonID', 4);
  });

  afterEach(() => http.verify());

  function load(unsched: object[], sched: object[]): void {
    fixture.detectChanges();
    http.expectOne('/api/seasons/4/matches/unscheduled').flush(unsched);
    http.expectOne('/api/seasons/4/matches/scheduled').flush(sched);
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  const idleRun: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };
  const noRecommendation: ScheduleRecommendationPageDto = { run: idleRun, latest: null };

  it('does not request matches when there is no season id', () => {
    fixture.componentRef.setInput('seasonID', 0);
    fixture.detectChanges();
    http.expectNone(() => true);
    expect(component.scheduledMatches()).toEqual([]);
  });

  it('groups scheduled matches into numbered weeks', () => {
    load([], [
      scheduled(1, '2026-01-07T19:00:00Z'),
      scheduled(2, '2026-01-08T20:00:00Z'),
      scheduled(3, '2026-01-14T19:00:00Z'),
    ]);

    expect(component.scheduledMatches()).toHaveSize(2);
    expect(component.scheduledMatches()[0].weekNumber).toBe(1);
    expect(component.scheduledMatches()[0].games.map(g => g.seasonMatchID)).toEqual([1, 2]);
    expect(component.scheduledMatches()[1].weekNumber).toBe(2);
    expect(component.scheduledMatches()[1].games.map(g => g.seasonMatchID)).toEqual([3]);

    const game = component.scheduledMatches()[0].games[0];
    expect(game.homeTeamName).toBe('Home Team1');
    expect(game.awayTeamName).toBe('Away Team1');
    expect(game.complete).toBeFalse();
    expect(DateTime.isDateTime(game.time)).toBeTrue();
    expect(game.time.toISO()).toBe(DateTime.fromISO('2026-01-07T19:00:00Z').toISO());
  });

  it('sorts weeks chronologically across a year boundary', () => {
    // Fed to the component out of order, and spanning the 2026/2027 year boundary:
    // Number("2026-52") / Number("2027-1") are both NaN, so a sort on that string would
    // leave the groups in whatever order the API/dictionary happened to return them.
    load([], [
      scheduled(3, '2027-01-04T19:00:00Z'), // last, but fed first
      scheduled(1, '2026-12-21T19:00:00Z'), // earliest week
      scheduled(2, '2026-12-28T19:00:00Z'), // middle week, last week of 2026
    ]);

    expect(component.scheduledMatches()).toHaveSize(3);
    expect(component.scheduledMatches().map(w => w.games.map(g => g.seasonMatchID))).toEqual([[1], [2], [3]]);
    expect(component.scheduledMatches().map(w => w.weekNumber)).toEqual([1, 2, 3]);
  });

  it('shows the lists once both have loaded, also when one fails', () => {
    fixture.detectChanges();
    const lists = () => (fixture.nativeElement as HTMLElement).querySelector('.sched-lists')!;
    expect(lists().classList).toContain('hud-waiting');
    http.expectOne('/api/seasons/4/matches/unscheduled').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(lists().classList).toContain('hud-waiting');
    http.expectOne('/api/seasons/4/matches/scheduled').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(lists().classList).not.toContain('hud-waiting');
  });

  it('renders weeks, matchups and unscheduled matches, striking completed ones', () => {
    load([unscheduled(9, true)], [scheduled(1, '2026-01-07T19:00:00Z')]);

    expect(text()).toContain('Week 1');
    expect(text()).toContain('Home Team1 vs Away Team1');
    expect(text()).toContain('Wednesday');
    expect(text()).toContain('Home Team9 vs Away Team9');
    const struck = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('span.strike')).map(s => s.textContent);
    expect(struck).toEqual(['Home Team9 vs Away Team9']);
  });

  it('hides the organizer controls from other users', () => {
    load([], []);
    expect(text()).not.toContain('Show Edit');
    expect(text()).not.toContain('Create Regular Season Matches');
  });

  it('lets organizers toggle edit mode, which shows a time form per match', () => {
    isEventOrganizer.set(true);
    load([unscheduled(9)], [scheduled(1, '2026-01-07T19:00:00Z')]);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('form')).toHaveSize(0);

    const toggle = Array.from(el.querySelectorAll('button')).find(b => b.textContent!.includes('Show Edit'))!;
    toggle.click();
    fixture.detectChanges();
    http.expectOne('/api/seasons/4/schedule-recommendation').flush(noRecommendation);

    expect(component.isEditing()).toBeTrue();
    expect(text()).toContain('Close Edit');
    expect(el.querySelectorAll('form')).toHaveSize(2);

    component.toggleEdit();
    expect(component.isEditing()).toBeFalse();
  });

  it('posts the new time and reloads both lists on success', () => {
    load([], []);
    const match = unscheduled(5);
    const time = DateTime.fromISO('2026-02-01T19:00:00Z');
    component.onSubmit(match, time);

    const req = http.expectOne('/api/season-matches/5/time');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ seasonMatchID: 5, time: '2026-02-01T19:00:00.000Z' });
    req.flush({});

    http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
  });

  it('reloads both lists even when saving the time fails', () => {
    load([unscheduled(5)], []);
    component.onSubmit(unscheduled(5), DateTime.fromISO('2026-02-01T19:00:00Z'));
    http.expectOne('/api/season-matches/5/time').flush('nope', { status: 500, statusText: 'Server Error' });

    http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
    expect(component.unscheduledMatches()).toEqual([]);
  });

  it('reloads each time matches are created again from the same dialog', () => {
    load([], []);
    const created = new Subject<void>();
    dialog.open.and.returnValue({ componentInstance: { regularMatchesCreated: created } } as unknown as MatDialogRef<unknown>);
    component.createRegularSeasonMatches();

    for (let i = 0; i < 2; i++) {
      created.next();
      http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
      http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
    }
    // Both reloads from the one dialog: it wasn't opened again
    expect(dialog.open).toHaveBeenCalledTimes(1);
  });

  it('opens the create-matches dialog for this season and reloads when matches are created', () => {
    load([], []);
    const created = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { regularMatchesCreated: created } } as unknown as MatDialogRef<unknown>);

    component.createRegularSeasonMatches();
    expect(dialog.open).toHaveBeenCalledWith(CreateRegularMatchesDialogComponent, { data: 4 });

    created.emit();
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([unscheduled(1)]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
    expect(component.unscheduledMatches()).toHaveSize(1);
  });

  it('renders the matches once they arrive, without a manual detectChanges', async () => {
    await fixture.whenStable();
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([unscheduled(1)]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([scheduled(2, '2026-03-02T19:00:00Z')]);
    await fixture.whenStable();

    expect(text()).toContain('Home Team1 vs Away Team1');
    expect(text()).toContain('Week 1');
    expect(text()).toContain('Home Team2 vs Away Team2');
  });

  it('stops listening to the create-matches dialog once the page is destroyed', () => {
    load([], []);
    const created = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { regularMatchesCreated: created } } as unknown as MatDialogRef<unknown>);
    component.createRegularSeasonMatches();
    expect(created.observed).toBeTrue();

    fixture.destroy();

    expect(created.observed).toBeFalse();
  });

  it('submits the time picked in a row without editing the loaded match', async () => {
    isEventOrganizer.set(true);
    load([], [scheduled(1, '2026-01-07T19:00:00Z')]);
    component.toggleEdit();
    http.expectOne('/api/seasons/4/schedule-recommendation').flush(noRecommendation);
    fixture.detectChanges();
    await fixture.whenStable();
    const before = component.scheduledMatches()[0].games[0];
    const original = before.time;

    const form = fixture.debugElement.query(By.directive(NgForm)).injector.get(NgForm);
    const picked = DateTime.fromISO('2026-01-09T20:00:00Z');
    form.controls['time'].setValue(picked);
    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));

    const req = http.expectOne('/api/season-matches/1/time');
    expect(req.request.body).toEqual({ seasonMatchID: 1, time: '2026-01-09T20:00:00.000Z' });
    expect(before.time).toBe(original);
    req.flush({});
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
  });

  it('shows each scheduled match day and time as the date pipe showed the API value', () => {
    load([], [scheduled(1, '2026-01-07T19:00:00Z')]);
    const pipe = new DatePipe('en-US');
    expect(text()).toContain(`${pipe.transform('2026-01-07T19:00:00Z', 'EEEE')} ${pipe.transform('2026-01-07T19:00:00Z', 'short')}`);
  });

  it('saves an unscheduled match without a time as null, not an empty string', async () => {
    isEventOrganizer.set(true);
    load([unscheduled(5)], []);
    component.toggleEdit();
    http.expectOne('/api/seasons/4/schedule-recommendation').flush(noRecommendation);
    fixture.detectChanges();
    await fixture.whenStable();

    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));

    const req = http.expectOne('/api/season-matches/5/time');
    expect(req.request.body).toEqual({ seasonMatchID: 5, time: null });
    req.flush({});
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
  });
  const recommendation = (changes: Partial<ScheduleRecommendationDto> = {}): ScheduleRecommendationDto => ({
    scheduleRecommendationID: 12, createdAt: '2026-11-01T18:00:00Z', requestedBy: 'Commissioner Grif', fitness: 0.5, generations: 2000,
    appliedAt: null, appliedBy: null,
    matches: [
      { seasonMatchID: 1, homeTeam: 'Reds', awayTeam: 'Blues', scheduledTime: '2026-11-03T01:00:00Z', currentTime: null, overlaps: false, homeTeamAvailable: true, awayTeamAvailable: true, decided: false },
      { seasonMatchID: 2, homeTeam: 'Reds', awayTeam: 'Greens', scheduledTime: '2026-11-03T01:15:00Z', currentTime: '2026-11-05T01:00:00Z', overlaps: true, homeTeamAvailable: true, awayTeamAvailable: false, decided: false },
      { seasonMatchID: 3, homeTeam: 'Blues', awayTeam: 'Greens', scheduledTime: '2026-11-04T01:00:00Z', currentTime: '2026-10-30T01:00:00Z', overlaps: false, homeTeamAvailable: true, awayTeamAvailable: true, decided: true },
    ],
    ...changes,
  });
  const button = (label: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find(b => b.textContent!.trim() === label);

  function openEditor(page: ScheduleRecommendationPageDto): void {
    isEventOrganizer.set(true);
    load([], []);
    component.toggleEdit();
    http.expectOne('/api/seasons/4/schedule-recommendation').flush(page);
    fixture.detectChanges();
  }

  it('words what is wrong with a recommended time', () => {
    const [fine, clash] = recommendation().matches;
    expect(conflictText(fine)).toBe('');
    expect(conflictText(clash)).toBe('Overlaps another match, Greens unavailable');
  });

  it('shows the latest recommendation to organizers while editing, each match with its conflicts', () => {
    openEditor({ run: idleRun, latest: recommendation() });
    const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.sched-recommend__table tbody tr'))
      .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.replace(/\s+/g, ' ').trim()));
    expect(rows.map(r => r[0])).toEqual(['Reds vs Blues', 'Reds vs Greens', 'Blues vs Greens · Final']);
    expect(rows[0][2]).toBe('Unscheduled');
    expect(rows.map(r => r[3])).toEqual(['-', 'Overlaps another match, Greens unavailable', '-']);
    expect(text()).toContain('1 of 3 matches with a conflict');
    expect(component.applicable()).toBe(2);
  });

  it('applies the times after a confirmation, then reads the schedule and the recommendation again', () => {
    openEditor({ run: idleRun, latest: recommendation() });
    button('Apply these times')!.click();
    fixture.detectChanges();
    expect(text()).toContain('Set 2 matches without a result to these times?');
    http.expectNone('/api/schedule-recommendations/12/apply');

    button('Confirm')!.click();
    http.expectOne({ method: 'POST', url: '/api/schedule-recommendations/12/apply' }).flush({ matchesScheduled: 2 });
    http.expectOne('/api/seasons/4/matches/unscheduled').flush([]);
    http.expectOne('/api/seasons/4/matches/scheduled').flush([]);
    http.expectOne('/api/seasons/4/schedule-recommendation').flush({ run: idleRun, latest: recommendation({ appliedAt: '2026-11-01T19:00:00Z', appliedBy: 'Commissioner Grif' }) });
    fixture.detectChanges();
    expect(text()).toContain('by Commissioner Grif');
    expect(button('Apply these times')).toBeUndefined();
  });

  it('cancels applying', () => {
    openEditor({ run: idleRun, latest: recommendation() });
    button('Apply these times')!.click();
    fixture.detectChanges();
    button('Cancel')!.click();
    fixture.detectChanges();
    expect(component.confirmingApply()).toBeFalse();
    http.expectNone('/api/schedule-recommendations/12/apply');
  });

  it('runs a recommendation on the worker and follows it until it has one', () => {
    jasmine.clock().install();
    try {
      openEditor(noRecommendation);
      expect(text()).not.toContain('matches with a conflict');
      button('Recommend times')!.click();
      http.expectOne({ method: 'POST', url: '/api/seasons/4/schedule-recommendation/run' }).flush({ ...idleRun, state: 'Queued' });
      http.expectOne('/api/seasons/4/schedule-recommendation').flush({ run: { ...idleRun, state: 'Queued' }, latest: null });
      fixture.detectChanges();
      expect(text()).toContain('Waiting for the worker');
      expect(button('Recommend times')!.disabled).toBeTrue();

      jasmine.clock().tick(RECOMMEND_POLL_MS);
      http.expectOne('/api/seasons/4/schedule-recommendation').flush({ run: { ...idleRun, state: 'Running', progress: 40, progressMessage: 'Generation 800, best fitness 0.500' }, latest: null });
      fixture.detectChanges();
      expect(text()).toContain('Working: Generation 800, best fitness 0.500');
      expect((fixture.nativeElement as HTMLElement).querySelector('progress')!.value).toBe(40);

      jasmine.clock().tick(RECOMMEND_POLL_MS);
      http.expectOne('/api/seasons/4/schedule-recommendation').flush({ run: idleRun, latest: recommendation() });
      fixture.detectChanges();
      expect(text()).toContain('1 of 3 matches with a conflict');
      expect(button('Recommend times')!.disabled).toBeFalse();

      jasmine.clock().tick(RECOMMEND_POLL_MS * 2);
      http.expectNone('/api/seasons/4/schedule-recommendation');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  describe('when the season changes', () => {
    it("loads the new season's schedule, and closes the editor with the previous one's recommendation", () => {
      jasmine.clock().install();
      try {
        openEditor({ run: { ...idleRun, state: 'Running' }, latest: recommendation() });
        // A run the server refused: its error stays until the season changes
        component.recommend();
        http.expectOne({ method: 'POST', url: '/api/seasons/4/schedule-recommendation/run' }).flush(null, { status: 500, statusText: 'Server Error' });
        expect(component.recommendationError()).toBeTruthy();
        component.confirmingApply.set(true);

        fixture.componentRef.setInput('seasonID', 5);
        fixture.detectChanges();
        expect(component.isEditing()).toBeFalse();
        expect(component.recommendation()).toBeNull();
        expect(component.recommendationError()).toBeNull();
        expect(component.confirmingApply()).toBeFalse();
        expect(component.loaded()).toBeFalse();

        http.expectOne('/api/seasons/5/matches/unscheduled').flush([unscheduled(9)]);
        http.expectOne('/api/seasons/5/matches/scheduled').flush([scheduled(1, '2026-01-07T19:00:00Z')]);
        fixture.detectChanges();
        expect(component.unscheduledMatches().map(m => m.seasonMatchID)).toEqual([9]);
        expect(component.scheduledMatches()[0].games.map(g => g.seasonMatchID)).toEqual([1]);

        // The previous season's run is no longer followed
        jasmine.clock().tick(RECOMMEND_POLL_MS * 2);
        http.expectNone('/api/seasons/4/schedule-recommendation');
        http.expectNone('/api/seasons/5/schedule-recommendation');
      } finally {
        jasmine.clock().uninstall();
      }
    });

    it("drops the previous season's lists and recommendation when they arrive late", () => {
      isEventOrganizer.set(true);
      fixture.detectChanges();
      const lateUnscheduled = http.expectOne('/api/seasons/4/matches/unscheduled');
      const lateScheduled = http.expectOne('/api/seasons/4/matches/scheduled');
      component.toggleEdit();
      const lateRecommendation = http.expectOne('/api/seasons/4/schedule-recommendation');

      fixture.componentRef.setInput('seasonID', 5);
      fixture.detectChanges();
      http.expectOne('/api/seasons/5/matches/unscheduled').flush([]);
      http.expectOne('/api/seasons/5/matches/scheduled').flush([]);
      lateUnscheduled.flush([unscheduled(9)]);
      lateScheduled.flush([scheduled(1, '2026-01-07T19:00:00Z')]);
      lateRecommendation.flush({ run: idleRun, latest: recommendation() });

      expect(component.unscheduledMatches()).toEqual([]);
      expect(component.scheduledMatches()).toEqual([]);
      expect(component.recommendation()).toBeNull();
    });

    it('creates matches and recommends for the season shown', () => {
      isEventOrganizer.set(true);
      fixture.componentRef.setInput('seasonID', 5);
      fixture.detectChanges();
      http.expectOne('/api/seasons/5/matches/unscheduled').flush([]);
      http.expectOne('/api/seasons/5/matches/scheduled').flush([]);
      dialog.open.and.returnValue({ componentInstance: { regularMatchesCreated: new EventEmitter<void>() } } as unknown as MatDialogRef<unknown>);

      component.createRegularSeasonMatches();
      expect(dialog.open.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ data: 5 }));

      component.recommend();
      http.expectOne({ method: 'POST', url: '/api/seasons/5/schedule-recommendation/run' }).flush({ ...idleRun, state: 'Queued' });
      http.expectOne('/api/seasons/5/schedule-recommendation').flush(noRecommendation);
    });
  });

  it('says why a recommendation could not be started, or why the last run failed', () => {
    openEditor({ run: { ...idleRun, lastRun: { firedAt: '2026-11-01T18:00:00Z', durationMs: 5, result: 'Failed', error: 'Season not found' } }, latest: null });
    expect(text()).toContain('The last run failed: Season not found');
    button('Recommend times')!.click();
    http.expectOne({ method: 'POST', url: '/api/seasons/4/schedule-recommendation/run' }).flush('Forbidden', { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(component.recommendationError()).toBeTruthy();
  });
  it('says when the recommendation could not be read, applied or followed', () => {
    jasmine.clock().install();
    try {
      isEventOrganizer.set(true);
      load([], []);
      component.toggleEdit();
      http.expectOne('/api/seasons/4/schedule-recommendation').flush('no', { status: 500, statusText: 'Server Error' });
      expect(component.recommendationError()).toBeTruthy();

      component.apply();
      http.expectNone('/api/schedule-recommendations/12/apply');

      component.loadRecommendation();
      http.expectOne('/api/seasons/4/schedule-recommendation').flush({ run: { ...idleRun, state: 'Running' }, latest: recommendation() });
      component.apply();
      http.expectOne('/api/schedule-recommendations/12/apply').flush(null, { status: 409, statusText: 'Conflict' });
      expect(component.recommendationError()).toBeTruthy();

      jasmine.clock().tick(RECOMMEND_POLL_MS);
      http.expectOne('/api/seasons/4/schedule-recommendation').flush('down', { status: 502, statusText: 'Bad Gateway' });
      expect(component.recommendationError()).toContain("Couldn't tell whether the recommendation finished");
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('counts a match with only its home team unavailable as a conflict', () => {
    const [fine] = recommendation().matches;
    expect(conflictText({ ...fine, homeTeamAvailable: false })).toBe('Reds unavailable');
  });
});
