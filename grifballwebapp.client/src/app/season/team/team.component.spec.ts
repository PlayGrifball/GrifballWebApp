import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { MatchDto, TeamComponent, TeamDto } from './team.component';

describe('TeamComponent', () => {
  let fixture: ComponentFixture<TeamComponent>;
  let component: TeamComponent;
  let http: HttpTestingController;

  const team: TeamDto = {
    teamName: 'Red Team',
    wins: 3,
    losses: 1,
    tbd: 2,
    players: [
      { teamPlayerID: 1, userID: 10, gamertag: 'Captain Grif', draftCaptainOrder: 1, draftRound: null },
      { teamPlayerID: 2, userID: 11, gamertag: 'Simmons', draftCaptainOrder: null, draftRound: 2 },
    ],
  };

  const match = (overrides: Partial<MatchDto>): MatchDto => ({
    seasonMatchID: 1,
    scheduledTime: '2026-04-01T19:00:00' as any,
    bestOf: 3,
    score: 2,
    result: 'Won',
    otherTeamID: 20,
    otherTeamName: 'Blue Team',
    otherScore: 1,
    otherResult: 'Loss',
    ...overrides,
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('teamID', 7);
    fixture.componentRef.setInput('seasonID', 2);
  });

  afterEach(() => http.verify());

  function load(t: TeamDto | null, matches: MatchDto[]): void {
    fixture.detectChanges();
    http.expectOne('/api/Team/Team/7').flush(t);
    http.expectOne('/api/Team/Matches/7').flush(matches);
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('tr.mat-mdc-row'))
    .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  it('shows the team name, record, remaining matches and roster', () => {
    load(team, []);
    const text = el().textContent!;

    expect(component.team()).toEqual(team);
    expect(el().querySelector('h1')!.textContent).toBe('Red Team');
    expect(text).toContain('3 - 1');
    expect(text).toContain('2 matches need to be played');
    expect(text).toMatch(/Captain Grif -\s+Captain/);
    expect(text).toMatch(/Simmons -\s+R2/);
  });

  it('hides the remaining-matches line when none are left', () => {
    load({ ...team, tbd: 0 }, []);
    expect(el().textContent).not.toContain('matches need to be played');
  });

  it('shows scores, TBD and N/A results in the schedule', () => {
    load(team, [
      match({ seasonMatchID: 1 }),
      match({ seasonMatchID: 2, result: null, score: null, otherScore: null, otherResult: null }),
      match({ seasonMatchID: 3, result: 'Won', score: null, otherScore: null, otherResult: 'Forfeit' }),
      match({ seasonMatchID: 4, result: 'Bye', score: null, otherScore: null, otherResult: null, otherTeamName: null }),
    ]);

    const r = rows();
    expect(r.length).toBe(4);
    expect(r[0].slice(0, 3)).toEqual(['Blue Team', '2 - 1', 'Won']);
    expect(r[1].slice(1, 3)).toEqual(['TBD', 'TBD']);
    expect(r[2].slice(1, 3)).toEqual(['N/A', 'Won']);
    expect(r[3].slice(1, 3)).toEqual(['N/A', 'Bye']);
    expect(r[0][3]).toMatch(/^4\/1\/26, 7:00\sPM$/);
  });

  it('links to the opposing team and the match page', () => {
    load(team, [match({ seasonMatchID: 99, otherTeamID: 20 })]);
    const urls = fixture.debugElement.queryAll(By.directive(RouterLink))
      .map(d => d.injector.get(RouterLink).urlTree!.toString());

    expect(urls).toEqual(['/season/2/team/20', '/seasonmatch/99']);
  });

  it('reloads when the team changes', () => {
    load(team, []);
    fixture.componentRef.setInput('teamID', 8);
    fixture.detectChanges();

    http.expectOne('/api/Team/Team/8').flush({ ...team, teamName: 'Other' });
    http.expectOne('/api/Team/Matches/8').flush([]);
    fixture.detectChanges();
    expect(el().querySelector('h1')!.textContent).toBe('Other');
  });
});
