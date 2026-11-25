import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { DraftPoolPlayer, DraftSuggestions } from '@api/models';
import { DraftHelperComponent } from './draftHelper.component';

describe('DraftHelperComponent', () => {
  let fixture: ComponentFixture<DraftHelperComponent>;
  let http: HttpTestingController;
  const pool: DraftPoolPlayer[] = [
    { userID: 7, name: 'Ace', region: 'West North America', willCaptain: true,
      timeslots: [{ availabilityOptionID: 1, dayOfWeek: 'Monday', time: '20:00:00' }, { availabilityOptionID: 2, dayOfWeek: 'Tuesday', time: '21:30:00' }],
      career: { seasons: 2, games: 20, winPercent: 80, goalsPerGame: 2, killsPerGame: 15, titles: 1 } },
    { userID: 8, name: 'Rookie', region: null, willCaptain: false, timeslots: [], career: null },
  ];
  const suggestions: DraftSuggestions = { teamID: 1, teamName: 'Reds', captainName: 'Cap',
    picks: [{ userID: 7, name: 'Ace', score: 0.675, reasons: ['Won 80% of 20 games', 'Same region as the captain'] }] };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DraftHelperComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DraftHelperComponent);
    fixture.componentRef.setInput('seasonID', 5);
    fixture.componentRef.setInput('draftKey', '7,8');
    fixture.detectChanges();
    http.expectOne('/api/seasons/5/draft/pool-details').flush(pool);
    http.expectOne('/api/seasons/5/draft/suggestions').flush(suggestions);
    await fixture.whenStable();
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  it('suggests picks for the team on deck, each with its score and reasons', () => {
    const picks = el().querySelector('.dh-picks')!.textContent!;
    expect(el().textContent).toContain('Suggested picks for Cap (Reds)');
    expect(picks).toContain('Ace');
    expect(picks).toContain('68');
    expect(picks).toContain('Same region as the captain');
  });

  it("shows each pool player's region, timeslots and career; one who never played says so", () => {
    const rows = Array.from(el().querySelectorAll('.dh-pool tbody tr')).map(r => r.textContent!.replace(/\s+/g, ' '));
    expect(rows[0]).toContain('Ace');
    expect(rows[0]).toContain('West North America');
    expect(rows[0]).toContain('Mon 20:00, Tue 21:30');
    expect(rows[0]).toContain('80%');
    expect(rows[0]).toContain('Would captain');
    expect(rows[1]).toContain('No recorded games');
  });

  it('loads again when the pool changes (a pick made), keeping the last shown meanwhile', async () => {
    fixture.componentRef.setInput('draftKey', '8');
    fixture.detectChanges();
    expect(el().querySelector('app-skeleton')).toBeNull();
    expect(el().querySelectorAll('.dh-pool tbody tr')).toHaveSize(2);
    http.expectOne('/api/seasons/5/draft/pool-details').flush([pool[1]]);
    http.expectOne('/api/seasons/5/draft/suggestions').flush(suggestions);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el().querySelectorAll('.dh-pool tbody tr')).toHaveSize(1);
  });
});
