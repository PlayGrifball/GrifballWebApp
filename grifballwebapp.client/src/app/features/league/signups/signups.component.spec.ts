import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { SignupsComponent } from './signups.component';
import { SeasonService } from '@api/services/season.service';
import { SignupsService } from '@api/services/signups.service';
import type { SignupResponseDto } from '@api/models';
import { SeasonSummaryService } from '../season/seasonSummary';
import type { SeasonSummaryDto } from '@api/models';

describe('SignupsComponent', () => {
  let fixture: ComponentFixture<SignupsComponent>;
  let component: SignupsComponent;
  let seasons: jasmine.SpyObj<SeasonService>;
  let api: jasmine.SpyObj<SignupsService>;
  let summary: Partial<SeasonSummaryDto>;

  const signup = (personName: string, overrides: Partial<SignupResponseDto> = {}): SignupResponseDto =>
    ({
      seasonID: 0, userID: 0, personName, timestamp: '2026-01-07T19:00:00Z', teamName: `${personName}'s team`, willCaptain: false,
      requiresAssistanceDrafting: false, region: null, timeslots: [], ...overrides,
    });

  beforeEach(async () => {
    summary = { isHistorical: false, hasEnded: false, signupCount: 0 };
    api = jasmine.createSpyObj<SignupsService>('SignupsService', ['getSignups']);
    seasons = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    api.getSignups.and.returnValue(of([]));
    seasons.getCurrentSeasonId.and.returnValue(of(9));

    await TestBed.configureTestingModule({
      imports: [SignupsComponent],
      providers: [
        provideRouter([]),
        { provide: SignupsService, useValue: api },
        { provide: SeasonService, useValue: seasons },
        { provide: SeasonSummaryService, useValue: { get: () => of(summary) } },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SignupsComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonID', '3');
  });

  const rows = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row'))
    .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  /** Drawn, with every answer in (the resources' values come a microtask after them). */
  async function render(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('says a backfilled season has no signup data, with its spreadsheet, in place of the table', async () => {
    summary = { isHistorical: true, hasEnded: true, signupCount: 0, historicalSourceUrl: 'https://docs.google.com/spreadsheets/d/x' };
    await render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-data-notice')!.textContent).toContain("Signups weren't recorded for this season.");
    expect(el.querySelector('app-data-notice a')!.getAttribute('href')).toBe('https://docs.google.com/spreadsheets/d/x');
    expect(el.querySelector('table')).toBeNull();
  });

  it("says an ended season of the app's own has no signups, without a spreadsheet", async () => {
    summary = { isHistorical: false, hasEnded: true, signupCount: 0, historicalSourceUrl: null };
    await render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-data-notice')!.textContent).toContain("Signups weren't recorded for this season.");
    expect(el.querySelector('app-data-notice a')).toBeNull();
    expect(el.querySelector('table')).toBeNull();
  });

  it("shows the table, empty, for a season of the app's own without signups yet", async () => {
    await render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-data-notice')).toBeNull();
    expect(el.querySelector('table')).not.toBeNull();
    expect(el.textContent).toContain('0 signed up');
  });

  it('shows a skeleton in place of the table until the signups arrive', async () => {
    const answer = new Subject<SignupResponseDto[]>();
    api.getSignups.and.returnValue(answer);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-skeleton')).not.toBeNull();
    expect(el.querySelector('table')).toBeNull();
    answer.next([signup('Grif')]);
    answer.complete();
    await render();
    expect(el.querySelector('app-skeleton')).toBeNull();
    expect(rows()).toHaveSize(1);
  });

  it('says the signups could not be loaded, rather than that none signed up, and logs it', async () => {
    const err = spyOn(console, 'error');
    api.getSignups.and.returnValue(throwError(() => 'boom'));
    await render();
    expect(err).toHaveBeenCalledWith('Failed to load the signups', 'boom');
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')!.textContent).toContain("The signups couldn't be loaded.");
    expect(el.textContent).not.toContain('signed up');
  });

  it('loads the signups for the season in the route', async () => {
    await render();
    expect(api.getSignups).toHaveBeenCalledWith({ seasonID: 3 });
    expect(seasons.getCurrentSeasonId).not.toHaveBeenCalled();
  });

  it('falls back to the current season when the route has none', async () => {
    fixture.componentRef.setInput('seasonID', '0');
    await render();
    expect(component.shownSeasonID()).toBe(9);
    expect(api.getSignups).toHaveBeenCalledWith({ seasonID: 9 });
  });

  it("replaces the signups with the new season's when the route moves to another season", async () => {
    api.getSignups.and.callFake(p => of([signup(p.seasonID === 3 ? 'Grif' : 'Simmons')]));
    await render();
    expect(rows().map(r => r[0])).toEqual([jasmine.stringContaining('Grif')]);

    fixture.componentRef.setInput('seasonID', '4');
    await render();

    expect(api.getSignups).toHaveBeenCalledWith({ seasonID: 4 });
    expect(component.shownSeasonID()).toBe(4);
    expect(rows().map(r => r[0])).toEqual([jasmine.stringContaining('Simmons')]);
    expect((fixture.nativeElement as HTMLElement).querySelector('app-season-tabs a')!.getAttribute('href')).toBe('/season/4');
  });

  it("cancels the previous season's signups when the route moves on before they arrive", async () => {
    const late = new Subject<SignupResponseDto[]>();
    api.getSignups.and.callFake(p => p.seasonID === 3 ? late : of([signup('Simmons')]));
    fixture.detectChanges();

    fixture.componentRef.setInput('seasonID', '4');
    fixture.detectChanges();
    expect(late.observed).toBeFalse();
    await render();

    expect(component.signUps().map(s => s.personName)).toEqual(['Simmons']);
  });

  it("asks for no current season once the route names one", async () => {
    const current = new Subject<number>();
    seasons.getCurrentSeasonId.and.returnValue(current);
    fixture.componentRef.setInput('seasonID', '0');
    fixture.detectChanges();

    fixture.componentRef.setInput('seasonID', '4');
    fixture.detectChanges();
    current.next(9);
    current.complete();
    await render();

    expect(api.getSignups).not.toHaveBeenCalledWith({ seasonID: 9 });
    expect(component.shownSeasonID()).toBe(4);
  });

  it('renders a row per signup', async () => {
    api.getSignups.and.returnValue(of([
      signup('Grif', { willCaptain: true }),
      signup('Simmons', { requiresAssistanceDrafting: true }),
    ]));
    await render();

    const r = rows();
    expect(r).toHaveSize(2);
    expect(r[0][0]).toBe('Grif');
    expect(r[0][2]).toBe("Grif's team");
    expect(r[0].slice(3)).toEqual(['true', 'false']);
    expect(r[1].slice(3)).toEqual(['false', 'true']);
  });

  it("shows a player's region beside their name", async () => {
    api.getSignups.and.returnValue(of([signup('Grif', { region: 'Central North America' }), signup('Simmons')]));
    await render();

    expect(rows()[0][0]).toBe('Grif · Central North America');
    expect(rows()[1][0]).toBe('Simmons');
  });

  it('renders the signups once they arrive, without a manual detectChanges after them', async () => {
    const signups = new Subject<SignupResponseDto[]>();
    api.getSignups.and.returnValue(signups);
    // Not whenStable before the answer: a resource still loading keeps the app unstable
    fixture.detectChanges();

    signups.next([signup('Ana')]);
    signups.complete();
    await fixture.whenStable();

    expect(rows().map(r => r[0])).toEqual(['Ana']);
  });

  it('shows when each player signed up', async () => {
    api.getSignups.and.returnValue(of([signup('Ana')]));
    await render();
    expect(rows()[0][1]).toMatch(/^1\/7\/26, \d+:00\s[AP]M$/);
  });
});
