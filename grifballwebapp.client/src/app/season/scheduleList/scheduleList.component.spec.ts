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
import { of } from 'rxjs';
import { provideAnimations } from '@angular/platform-browser/animations';
import { JWT_OPTIONS, JwtHelperService } from '@auth0/angular-jwt';

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
        { provide: JWT_OPTIONS, useValue: {} },
        JwtHelperService,
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
    seasonMatchID: id, homeCaptain: `Home${id}`, awayCaptain: `Away${id}`, complete, time: null as any
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
    expect(component.scheduledMatches).toEqual([]);
  });

  it('groups scheduled matches into numbered weeks', () => {
    load([], [
      scheduled(1, '2026-01-07T19:00:00'),
      scheduled(2, '2026-01-08T20:00:00'),
      scheduled(3, '2026-01-14T19:00:00'),
    ]);

    expect(component.scheduledMatches.length).toBe(2);
    expect(component.scheduledMatches[0].weekNumber).toBe(1);
    expect(component.scheduledMatches[0].games.map(g => g.seasonMatchID)).toEqual([1, 2]);
    expect(component.scheduledMatches[1].weekNumber).toBe(2);
    expect(component.scheduledMatches[1].games.map(g => g.seasonMatchID)).toEqual([3]);

    const game = component.scheduledMatches[0].games[0];
    expect(game.homeCaptainName).toBe('Home1');
    expect(game.awayCaptainName).toBe('Away1');
    expect(game.complete).toBeFalse();
    expect(DateTime.isDateTime(game.time)).toBeTrue();
    expect(game.time.toISO()).toContain('2026-01-07T19:00:00');
  });

  it('renders weeks, matchups and unscheduled matches, striking completed ones', () => {
    load([unscheduled(9, true)], [scheduled(1, '2026-01-07T19:00:00')]);

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
    load([unscheduled(9)], [scheduled(1, '2026-01-07T19:00:00')]);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('form').length).toBe(0);

    const toggle = Array.from(el.querySelectorAll('button')).find(b => b.textContent!.includes('Show Edit'))!;
    toggle.click();
    fixture.detectChanges();

    expect(component.isEditing).toBeTrue();
    expect(text()).toContain('Close Edit');
    expect(el.querySelectorAll('form').length).toBe(2);

    component.toggleEdit();
    expect(component.isEditing).toBeFalse();
  });

  it('posts the new time and reloads both lists on success', () => {
    load([], []);
    const match = unscheduled(5);
    component.onSubmit(match);

    const req = http.expectOne('/api/MatchPlanner/UpdateMatchTime/');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBe(match);
    req.flush({});

    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
  });

  it('reloads both lists even when saving the time fails', () => {
    load([unscheduled(5)], []);
    component.onSubmit(unscheduled(5));
    http.expectOne('/api/MatchPlanner/UpdateMatchTime/').flush('nope', { status: 500, statusText: 'Server Error' });

    http.expectOne('/api/MatchPlanner/GetUnscheduledMatches/4').flush([]);
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([]);
    expect(component.unscheduledMatches).toEqual([]);
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
    expect(component.unscheduledMatches.length).toBe(1);
  });
});
