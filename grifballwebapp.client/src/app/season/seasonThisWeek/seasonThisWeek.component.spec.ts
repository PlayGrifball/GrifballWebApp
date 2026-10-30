import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { DateTime } from 'luxon';
import { pickThisWeek, SeasonThisWeekComponent } from './seasonThisWeek.component';
import { ScheduledMatchDto } from '../scheduleList/scheduledMatchDto';

// Wednesday of a week; matches on the Monday and Friday of it are "this week".
const now = DateTime.fromISO('2026-03-11T12:00:00');
const match = (id: number, iso: string, complete = false): ScheduledMatchDto =>
  ({ seasonMatchID: id, homeCaptain: `Home ${id}`, awayCaptain: `Away ${id}`, complete, time: DateTime.fromISO(iso) });

describe('pickThisWeek', () => {
  it("shows this week's matches in time order, each with its status", () => {
    const picked = pickThisWeek([
      match(3, '2026-03-13T20:00:00'),
      match(1, '2026-03-09T20:00:00', true),
      match(2, '2026-03-10T20:00:00'),
      match(9, '2026-03-20T20:00:00'),
    ], now);
    expect(picked.map(p => [p.match.seasonMatchID, p.status])).toEqual([[1, 'final'], [2, 'awaiting'], [3, 'upcoming']]);
  });

  it('shows the next three matches between weeks', () => {
    const picked = pickThisWeek([
      match(1, '2026-03-01T20:00:00', true),
      match(5, '2026-04-05T20:00:00'),
      match(2, '2026-03-20T20:00:00'),
      match(3, '2026-03-21T20:00:00'),
      match(4, '2026-03-28T20:00:00'),
    ], now);
    expect(picked.map(p => p.match.seasonMatchID)).toEqual([2, 3, 4]);
    expect(picked.every(p => p.status === 'upcoming')).toBeTrue();
  });

  it('shows the last three matches once the season is over', () => {
    const picked = pickThisWeek([1, 2, 3, 4].map(i => match(i, `2026-02-0${i}T20:00:00`, true)), now);
    expect(picked.map(p => p.match.seasonMatchID)).toEqual([2, 3, 4]);
  });

  it('shows nothing without matches', () => {
    expect(pickThisWeek([], now)).toEqual([]);
  });
});

describe('SeasonThisWeekComponent', () => {
  let fixture: ComponentFixture<SeasonThisWeekComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonThisWeekComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonThisWeekComponent);
    fixture.componentInstance.now.set(now);
  });

  afterEach(() => http.verify());

  const text = () => (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');

  it('loads the scheduled matches and links each card to its match', () => {
    fixture.componentRef.setInput('seasonID', 4);
    fixture.detectChanges();
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush([
      { seasonMatchID: 31, homeCaptain: 'Grif', awayCaptain: 'Simmons', complete: false, time: '2026-03-13T20:00:00Z' },
    ]);
    fixture.detectChanges();

    expect(text()).toContain('GrifVSSimmons');
    expect(text()).toContain('Upcoming');
    const hrefs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a')).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(['/seasonmatch/31', '/season/4?tab=schedule']);
  });

  it('says so when nothing is scheduled, also when the request fails', () => {
    fixture.componentRef.setInput('seasonID', 4);
    fixture.detectChanges();
    http.expectOne('/api/MatchPlanner/GetScheduledMatches/4').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(text()).toContain('No matches scheduled yet.');
  });

  it('waits for a season before loading anything', () => {
    fixture.componentRef.setInput('seasonID', 0);
    fixture.detectChanges();
    http.expectNone(() => true);
  });
});
