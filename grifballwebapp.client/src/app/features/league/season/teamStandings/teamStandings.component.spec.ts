import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TeamStandingsComponent } from './teamStandings.component';

describe('TeamStandingsComponent', () => {
  let fixture: ComponentFixture<TeamStandingsComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamStandingsComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamStandingsComponent);
    fixture.componentRef.setInput('seasonID', 2);
  });

  afterEach(() => http.verify());

  const rows = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row'))
    .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  it('does not request standings without a season', () => {
    fixture.componentRef.setInput('seasonID', 0);
    fixture.detectChanges();
    http.expectNone(() => true);
    expect(fixture.componentInstance.loaded()).toBeTrue();
    expect(rows()).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No standings yet.');
  });

  it('renders one row per team', async () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/2/standings').flush([
      { teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 },
      { teamID: 11, teamName: 'Bravo', wins: 2, losses: 3 },
    ]);
    await fixture.whenStable();

    expect(rows()).toEqual([['1', 'Alpha', '4', '1'], ['2', 'Bravo', '2', '3']]);
    // The leader is marked out.
    const leaders = (fixture.nativeElement as HTMLElement).querySelectorAll('tr.standings__leader');
    expect(leaders).toHaveSize(1);
    expect(leaders[0].textContent).toContain('Alpha');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('No standings yet.');
  });

  it("says the records are the source spreadsheet's when they are", async () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/2/standings').flush([
      { teamID: 10, teamName: 'Alpha', wins: 9, losses: 1, recordFromSource: true },
    ]);
    await fixture.whenStable();
    expect(rows()).toEqual([['1', 'Alpha', '9', '1']]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Records from the source spreadsheet');
  });

  it("shows each team's final place from the source spreadsheet, and none for a team it doesn't place", async () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/2/standings').flush([
      { teamID: 10, teamName: 'Defiance', wins: 14, losses: 6, recordFromSource: true, finish: 3 },
      { teamID: 11, teamName: 'Monstars', wins: 15, losses: 5, recordFromSource: true, finish: 1 },
      { teamID: 12, teamName: 'Hypest', wins: 13, losses: 7, recordFromSource: true, finish: 22 },
      { teamID: 13, teamName: 'Unplaced', wins: 2, losses: 18, recordFromSource: true, finish: null },
    ]);
    await fixture.whenStable();

    expect(rows()).toEqual([
      ['1', 'Defiance', '14', '6', '3rd'],
      ['2', 'Monstars', '15', '5', '1st'],
      ['3', 'Hypest', '13', '7', '22nd'],
      ['4', 'Unplaced', '2', '18', ''],
    ]);
    expect((fixture.nativeElement as HTMLElement).querySelector('th.standings__finish')?.textContent?.trim()).toBe('Finish');
  });

  it('has no finish column when no team has a final place', async () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/2/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1, finish: null }]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('th.standings__finish')).toBeNull();
  });

  it('names places as ordinals', () => {
    expect([1, 2, 3, 4, 11, 12, 13, 21, 22, 23, 101, 111].map(TeamStandingsComponent.ordinal))
      .toEqual(['1st', '2nd', '3rd', '4th', '11th', '12th', '13th', '21st', '22nd', '23rd', '101st', '111th']);
  });

  it('has no note for records from matches', async () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/2/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Records from the source spreadsheet');
  });

  it('logs and leaves the table empty when the request fails', async () => {
    const log = spyOn(console, 'error');
    fixture.detectChanges();
    // Not "no standings" while they load: a skeleton under the header
    expect(fixture.componentInstance.loaded()).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('No standings yet.');
    expect((fixture.nativeElement as HTMLElement).querySelector('app-skeleton')).not.toBeNull();
    http.expectOne('/api/seasons/2/standings').flush('x', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-skeleton')).toBeNull();

    expect(log).toHaveBeenCalledWith('Failed to load the standings', jasmine.anything());
    expect(fixture.componentInstance.loaded()).toBeTrue();
    expect(rows()).toEqual([]);
  });

  it("replaces the standings with the new season's when the season changes", async () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/2/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    await fixture.whenStable();

    fixture.componentRef.setInput('seasonID', 3);
    fixture.detectChanges();
    // Nothing of season 2 while season 3's load
    expect(rows()).toEqual([]);
    expect(fixture.componentInstance.loaded()).toBeFalse();

    http.expectOne('/api/seasons/3/standings').flush([{ teamID: 20, teamName: 'Charlie', wins: 1, losses: 0 }]);
    await fixture.whenStable();
    expect(rows()).toEqual([['1', 'Charlie', '1', '0']]);
  });

  it("cancels the previous season's request, so its standings can't arrive late", async () => {
    fixture.detectChanges();
    const late = http.expectOne('/api/seasons/2/standings');
    fixture.componentRef.setInput('seasonID', 3);
    fixture.detectChanges();

    http.expectOne('/api/seasons/3/standings').flush([{ teamID: 20, teamName: 'Charlie', wins: 1, losses: 0 }]);
    expect(late.cancelled).toBeTrue();
    await fixture.whenStable();

    expect(rows()).toEqual([['1', 'Charlie', '1', '0']]);
  });

  it('renders the standings once they arrive, without a manual detectChanges', async () => {
    // The first render, scheduled on its own, starts the request (whenStable would wait for the loading standings)
    await new Promise(resolve => setTimeout(resolve));
    http.expectOne('/api/seasons/2/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    await fixture.whenStable();

    expect(rows()).toEqual([['1', 'Alpha', '4', '1']]);
  });
});

describe('TeamStandingsComponent links', () => {
  it('links each team to its page under the current season', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([{ path: 'season/:seasonID', component: TeamStandingsComponent }], withComponentInputBinding()),
      ]
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/season/2', TeamStandingsComponent);
    TestBed.inject(HttpTestingController).expectOne('/api/seasons/2/standings')
      .flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    await harness.fixture.whenStable();
    harness.detectChanges();

    const link = harness.routeNativeElement!.querySelector('td a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/season/2/team/10');
  });
});
