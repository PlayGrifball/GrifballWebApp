import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { SignupsComponent } from './signups.component';
import { ApiClientService } from '../api/apiClient.service';
import { SignupResponseDto } from '../api/dtos/signupResponseDto';
import { SeasonSummaryDto, SeasonSummaryService } from '../season/seasonSummary';

describe('SignupsComponent', () => {
  let fixture: ComponentFixture<SignupsComponent>;
  let component: SignupsComponent;
  let api: jasmine.SpyObj<ApiClientService>;
  let seasonID: string | null;
  let summary: Partial<SeasonSummaryDto>;

  const signup = (personName: string, overrides: Partial<SignupResponseDto> = {}): SignupResponseDto =>
    Object.assign(new SignupResponseDto(), { personName, timestamp: '2026-01-07T19:00:00Z', teamName: `${personName}'s team`, ...overrides });

  beforeEach(async () => {
    seasonID = '3';
    summary = { isHistorical: false, signupCount: 0 };
    api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID', 'getSignups']);
    api.getSignups.and.returnValue(of([]));
    api.getCurrentSeasonID.and.returnValue(of(9));

    await TestBed.configureTestingModule({
      imports: [SignupsComponent],
      providers: [
        { provide: ApiClientService, useValue: api },
        { provide: SeasonSummaryService, useValue: { get: () => of(summary) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => seasonID } } } },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SignupsComponent);
    component = fixture.componentInstance;
  });

  const rows = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row'))
    .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  it('says a backfilled season has no signup data, with its spreadsheet, in place of the table', () => {
    summary = { isHistorical: true, signupCount: 0, historicalSourceUrl: 'https://docs.google.com/spreadsheets/d/x' };
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-data-notice')!.textContent).toContain("signup data isn't available");
    expect(el.querySelector('app-data-notice a')!.getAttribute('href')).toBe('https://docs.google.com/spreadsheets/d/x');
    expect(el.querySelector('table')).toBeNull();
  });

  it('shows the table, empty, for a season of the app's own without signups yet', () => {
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-data-notice')).toBeNull();
    expect(el.querySelector('table')).not.toBeNull();
  });

  it('loads the signups for the season in the route', () => {
    fixture.detectChanges();
    expect(api.getSignups).toHaveBeenCalledWith(3);
    expect(api.getCurrentSeasonID).not.toHaveBeenCalled();
  });

  it('falls back to the current season when the route has none', () => {
    seasonID = null;
    fixture.detectChanges();
    expect(component.seasonID).toBe(9);
    expect(api.getSignups).toHaveBeenCalledWith(9);
  });

  it('renders a row per signup', () => {
    api.getSignups.and.returnValue(of([
      signup('Grif', { willCaptain: true }),
      signup('Simmons', { requiresAssistanceDrafting: true }),
    ]));
    fixture.detectChanges();

    const r = rows();
    expect(r.length).toBe(2);
    expect(r[0][0]).toBe('Grif');
    expect(r[0][2]).toBe("Grif's team");
    expect(r[0].slice(3)).toEqual(['true', 'false']);
    expect(r[1].slice(3)).toEqual(['false', 'true']);
  });

  it('logs and shows an empty table when loading fails', () => {
    const err = spyOn(console, 'error');
    api.getSignups.and.returnValue(throwError(() => 'boom'));
    fixture.detectChanges();

    expect(err).toHaveBeenCalledWith('boom');
    expect(rows()).toEqual([]);
  });

  it('renders the signups once they arrive, without a manual detectChanges', async () => {
    const signups = new Subject<SignupResponseDto[]>();
    api.getSignups.and.returnValue(signups);
    await fixture.whenStable();

    signups.next([signup('Ana')]);
    await fixture.whenStable();

    expect(rows().map(r => r[0])).toEqual(['Ana']);
  });

  it('shows when each player signed up', async () => {
    api.getSignups.and.returnValue(of([signup('Ana')]));
    await fixture.whenStable();
    expect(rows()[0][1]).toMatch(/^1\/7\/26, \d+:00\s[AP]M$/);
  });
});
