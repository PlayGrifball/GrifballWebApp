import { DatePipe } from '@angular/common';
import { By } from '@angular/platform-browser';
import { NgForm } from '@angular/forms';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ScheduleListComponent } from './scheduleList.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { EventEmitter, signal, WritableSignal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { DateTime } from 'luxon';
import { AccountService } from '../../account.service';
import { CreateRegularMatchesDialogComponent } from './createRegularMatchesDialog/createRegularMatchesDialog.component';
import { of, Subject } from 'rxjs';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('ScheduleListComponent', () => {
  let component: ScheduleListComponent;
  let fixture: ComponentFixture<ScheduleListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScheduleListComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            parent: {
              params: of({ id: 'test-season-id' })
            }
          }
        }
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
  let seasonID: string | null;

  const unscheduled = (id: number, complete = false) => ({
    seasonMatchID: id, homeCaptain: `Home${id}`, awayCaptain: `Away${id}`, complete
  });
  const scheduled = (id: number, iso: string, complete = false) => ({
    seasonMatchID: id, homeCaptain: `Home${id}`, awayCaptain: `Away${id}`, complete, time: iso as any
  });

  beforeEach(async () => {
    seasonID = '4';
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
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => seasonID } } } },
      ]
    })
      .overrideProvider(MatDialog, { useValue: dialog })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ScheduleListComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => http.verify());

  function load(unsched: any[], sched: any[]): void {
    fixture.detectChanges();
    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush(unsched);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush(sched);
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('does not request matches when there is no season id', () => {
    seasonID = null;
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

    expect(component.scheduledMatches().length).toBe(2);
    expect(component.scheduledMatches()[0].weekNumber).toBe(1);
    expect(component.scheduledMatches()[0].games.map(g => g.seasonMatchID)).toEqual([1, 2]);
    expect(component.scheduledMatches()[1].weekNumber).toBe(2);
    expect(component.scheduledMatches()[1].games.map(g => g.seasonMatchID)).toEqual([3]);

    const game = component.scheduledMatches()[0].games[0];
    expect(game.homeCaptainName).toBe('Home1');
    expect(game.awayCaptainName).toBe('Away1');
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

    expect(component.scheduledMatches().length).toBe(3);
    expect(component.scheduledMatches().map(w => w.games.map(g => g.seasonMatchID))).toEqual([[1], [2], [3]]);
    expect(component.scheduledMatches().map(w => w.weekNumber)).toEqual([1, 2, 3]);
  });

  it('shows the lists once both have loaded, also when one fails', () => {
    fixture.detectChanges();
    const lists = () => (fixture.nativeElement as HTMLElement).querySelector('.sched-lists')!;
    expect(lists().classList).toContain('hud-waiting');
    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(lists().classList).toContain('hud-waiting');
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(lists().classList).not.toContain('hud-waiting');
  });

  it('renders weeks, matchups and unscheduled matches, striking completed ones', () => {
    load([unscheduled(9, true)], [scheduled(1, '2026-01-07T19:00:00Z')]);

    expect(text()).toContain('Week 1');
    expect(text()).toContain('Home1 vs Away1');
    expect(text()).toContain('Wednesday');
    expect(text()).toContain('Home9 vs Away9');
    const struck = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('span.strike')).map(s => s.textContent);
    expect(struck).toEqual(['Home9 vs Away9']);
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
    expect(el.querySelectorAll('form').length).toBe(0);

    const toggle = Array.from(el.querySelectorAll('button')).find(b => b.textContent!.includes('Show Edit'))!;
    toggle.click();
    fixture.detectChanges();

    expect(component.isEditing()).toBeTrue();
    expect(text()).toContain('Close Edit');
    expect(el.querySelectorAll('form').length).toBe(2);

    component.toggleEdit();
    expect(component.isEditing()).toBeFalse();
  });

  it('posts the new time and reloads both lists on success', () => {
    load([], []);
    const match = unscheduled(5);
    const time = DateTime.fromISO('2026-02-01T19:00:00Z');
    component.onSubmit(match, time);

    const req = http.expectOne('/api/MatchPlanner/UpdateMatchTime/');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ seasonMatchID: 5, time });
    req.flush({});

    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
  });

  it('reloads both lists even when saving the time fails', () => {
    load([unscheduled(5)], []);
    component.onSubmit(unscheduled(5), DateTime.fromISO('2026-02-01T19:00:00Z'));
    http.expectOne('/api/MatchPlanner/UpdateMatchTime/').flush('nope', { status: 500, statusText: 'Server Error' });

    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
    expect(component.unscheduledMatches()).toEqual([]);
  });

  it('reloads each time matches are created again from the same dialog', () => {
    load([], []);
    const created = new Subject<void>();
    dialog.open.and.returnValue({ componentInstance: { regularMatchesCreated: created } } as any);
    component.createRegularSeasonMatches();

    for (let i = 0; i < 2; i++) {
      created.next();
      http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([]);
      http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
    }
  });

  it('opens the create-matches dialog for this season and reloads when matches are created', () => {
    load([], []);
    const created = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { regularMatchesCreated: created } } as any);

    component.createRegularSeasonMatches();
    expect(dialog.open).toHaveBeenCalledWith(CreateRegularMatchesDialogComponent, { data: 4 });

    created.emit();
    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([unscheduled(1)]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
    expect(component.unscheduledMatches().length).toBe(1);
  });

  it('renders the matches once they arrive, without a manual detectChanges', async () => {
    await fixture.whenStable();
    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([unscheduled(1)]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([scheduled(2, '2026-03-02T19:00:00Z')]);
    await fixture.whenStable();

    expect(text()).toContain('Home1 vs Away1');
    expect(text()).toContain('Week 1');
    expect(text()).toContain('Home2 vs Away2');
  });

  it('stops listening to the create-matches dialog once the page is destroyed', () => {
    load([], []);
    const created = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { regularMatchesCreated: created } } as any);
    component.createRegularSeasonMatches();
    expect(created.observed).toBeTrue();

    fixture.destroy();

    expect(created.observed).toBeFalse();
  });

  it('submits the time picked in a row without editing the loaded match', async () => {
    isEventOrganizer.set(true);
    load([], [scheduled(1, '2026-01-07T19:00:00Z')]);
    component.toggleEdit();
    fixture.detectChanges();
    await fixture.whenStable();
    const before = component.scheduledMatches()[0].games[0];
    const original = before.time;

    const form = fixture.debugElement.query(By.directive(NgForm)).injector.get(NgForm);
    const picked = DateTime.fromISO('2026-01-09T20:00:00Z');
    form.controls['time'].setValue(picked);
    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));

    const req = http.expectOne('/api/MatchPlanner/UpdateMatchTime/');
    expect(req.request.body).toEqual({ seasonMatchID: 1, time: picked });
    expect(before.time).toBe(original);
    req.flush({});
    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
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
    fixture.detectChanges();
    await fixture.whenStable();

    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));

    const req = http.expectOne('/api/MatchPlanner/UpdateMatchTime/');
    expect(req.request.body).toEqual({ seasonMatchID: 5, time: null });
    req.flush({});
    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
  });
});
