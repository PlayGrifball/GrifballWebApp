import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TeamStandingsComponent } from './teamStandings.component';

describe('TeamStandingsComponent', () => {
  let fixture: ComponentFixture<TeamStandingsComponent>;
  let http: HttpTestingController;
  let seasonID: string | null;

  beforeEach(async () => {
    seasonID = '2';
    await TestBed.configureTestingModule({
      imports: [TeamStandingsComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => seasonID } } } },
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamStandingsComponent);
  });

  afterEach(() => http.verify());

  const rows = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tr.mat-mdc-row'))
    .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  it('does not request standings without a season', () => {
    seasonID = null;
    fixture.detectChanges();
    http.expectNone(() => true);
    expect(rows()).toEqual([]);
  });

  it('renders one row per team', () => {
    fixture.detectChanges();
    http.expectOne('api/TeamStandings/GetTeamStandings/2').flush([
      { teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 },
      { teamID: 11, teamName: 'Bravo', wins: 2, losses: 3 },
    ]);
    fixture.detectChanges();

    expect(rows()).toEqual([['Alpha', '4', '1'], ['Bravo', '2', '3']]);
  });

  it('logs and leaves the table empty when the request fails', () => {
    const log = spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('api/TeamStandings/GetTeamStandings/2').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(log).toHaveBeenCalled();
    expect(rows()).toEqual([]);
  });
});

describe('TeamStandingsComponent links', () => {
  it('links each team to its page under the current season', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([{ path: 'season/:seasonID', component: TeamStandingsComponent }]),
      ]
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/season/2', TeamStandingsComponent);
    TestBed.inject(HttpTestingController).expectOne('api/TeamStandings/GetTeamStandings/2')
      .flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    harness.detectChanges();

    const link = harness.routeNativeElement!.querySelector('td a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/season/2/team/10');
  });
});
