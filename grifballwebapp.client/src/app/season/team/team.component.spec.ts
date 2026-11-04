import { DateTime } from 'luxon';
import { DATE_PIPE_DEFAULT_OPTIONS, DatePipe } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, input, signal } from '@angular/core';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { MatchDto, TeamComponent, TeamDto } from './team.component';
import { seasonInfo } from '../../api/dtos/seasonInfoDto.stub';
import { SeasonPlayerStatsComponent } from '../seasonPlayerStats/seasonPlayerStats.component';
import { VodListComponent } from '../../shared/vodList/vodList.component';
import { StubVodListComponent } from '../../shared/vodList/vodList.stub';

/** The season's player stats table (tested on its own): here only what the team page gives it and reads back. */
@Component({ selector: 'app-season-player-stats', template: '' })
class StubPlayerStats {
  readonly seasonID = input<number>();
  readonly teamID = input<number | null>();
  readonly hasRows = signal(false);
}

describe('TeamComponent', () => {
  let fixture: ComponentFixture<TeamComponent>;
  let component: TeamComponent;
  let http: HttpTestingController;

  const team: TeamDto = {
    teamName: 'Red Team',
    season: seasonInfo(1, { hasEnded: false }),
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
    scheduledTime: '2026-04-01T19:00:00Z' as any, // as the API sends it
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
      providers: [
        provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]),
        // Times show in UTC here, so the expected strings hold on any machine's timezone.
        { provide: DATE_PIPE_DEFAULT_OPTIONS, useValue: { timezone: 'UTC' } },
      ]
    })
      .overrideComponent(TeamComponent, { remove: { imports: [SeasonPlayerStatsComponent, VodListComponent] }, add: { imports: [StubPlayerStats, StubVodListComponent] } })
      .compileComponents();

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
  const stats = () => fixture.debugElement.query(By.directive(StubPlayerStats)).componentInstance as StubPlayerStats;
  const rows = () => Array.from(el().querySelectorAll('tr.mat-mdc-row'))
    .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));

  it('shows the team name, record, remaining matches and roster', () => {
    load(team, []);
    const text = el().textContent!;

    expect(component.team()).toEqual(team);
    expect(el().querySelector('h1')!.textContent).toBe('Red Team');
    expect(text).toContain('3 - 1');
    expect(text).toContain('2 matches need to be played');
    const roster = Array.from(el().querySelectorAll('.team-player')).map(p => Array.from(p.children).map(c => c.textContent!.trim()).join(' '));
    expect(roster).toEqual(['C Captain Grif Captain', 'S Simmons R2']);
  });

  it("links to the franchise's history across seasons, by the team's league and name", () => {
    load({ ...team, teamName: 'Trophy Hunters' }, []);
    const link = fixture.debugElement.query(By.css('a.team-history-link'));

    expect(link.injector.get(RouterLink).urlTree!.toString()).toBe('/history/agla/team/Trophy%20Hunters');
  });

  it('has no history link before the team has loaded', () => {
    fixture.detectChanges();
    expect(el().querySelector('a.team-history-link')).toBeNull();
    http.expectOne('/api/Team/Team/7').flush(null);
    http.expectOne('/api/Team/Matches/7').flush([]);
  });

  it("lists the team's VODs", () => {
    load(team, []);
    const vods = fixture.debugElement.query(By.directive(StubVodListComponent)).componentInstance as StubVodListComponent;
    expect(vods.scope()).toBe('team');
    expect(vods.id()).toBe(7);
  });

  it('shows a player without a gamertag (no Xbox account linked) without breaking the page', () => {
    load({ ...team, players: [{ teamPlayerID: 3, userID: 12, gamertag: null, draftCaptainOrder: null, draftRound: 1 }] }, []);
    const roster = Array.from(el().querySelectorAll('.team-player')).map(p => Array.from(p.children).map(c => c.textContent!.trim()).join(' '));
    expect(roster).toEqual(['? Unknown Spartan R1']);
  });

  it("shows a player's region on the roster", () => {
    load({ ...team, players: [{ teamPlayerID: 3, userID: 12, gamertag: 'Sarge', draftCaptainOrder: null, draftRound: 1, region: 'East North America' }] }, []);
    expect(el().querySelector('.team-player__region')!.textContent!.trim()).toBe('East North America');
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

  it('shows a won playoff bye as a win against BYE, without a team link', () => {
    load(team, [match({ seasonMatchID: 5, score: null, otherScore: null, otherResult: 'Bye', otherTeamID: null, otherTeamName: null })]);
    const urls = fixture.debugElement.query(By.css('table')).queryAll(By.directive(RouterLink))
      .map(d => d.injector.get(RouterLink).urlTree!.toString());

    expect(rows()[0].slice(0, 3)).toEqual(['BYE', 'N/A', 'Won']);
    expect(urls).toEqual(['/seasonmatch/5']);
  });

  it('links to the opposing team and the match page', () => {
    load(team, [match({ seasonMatchID: 99, otherTeamID: 20 })]);
    const urls = fixture.debugElement.query(By.css('table')).queryAll(By.directive(RouterLink))
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
    expect(stats().teamID()).toBe(8);
  });

  it("gives the stats table the team and its season, and titles it only when the team has stats", () => {
    load(team, []);
    expect(stats().seasonID()).toBe(2);
    expect(stats().teamID()).toBe(7);
    expect(el().querySelector('#team-stats')).toBeNull();

    stats().hasRows.set(true);
    fixture.detectChanges();
    expect(el().querySelector('#team-stats')!.textContent).toBe('Player stats');
  });

  it('shows the empty match table for a season still going', () => {
    load(team, []);
    expect(el().querySelector('app-data-notice')).toBeNull();
    expect(el().querySelector('table[mat-table]')).not.toBeNull();
  });

  it("says an ended season's matches were not recorded, without a spreadsheet, in place of an empty table", () => {
    load({ ...team, season: seasonInfo(1) }, []);
    expect(el().querySelector('app-data-notice')!.textContent).toContain("Match results weren't recorded for this season.");
    expect(el().querySelector('app-data-notice a')).toBeNull();
    expect(el().querySelector('table[mat-table]')).toBeNull();
    expect(el().querySelector('app-backfilled-banner')).toBeNull();
  });

  it('shows each match time as the date pipe showed the API value', () => {
    load(team, [match({})]);

    expect(component.matches.data[0].scheduledTime instanceof DateTime).toBeTrue();
    expect(rows()[0]).toContain(new DatePipe('en-US', 'UTC').transform('2026-04-01T19:00:00Z', 'short')!);
  });

  describe('a team of a season backfilled from historical records', () => {
    const url = 'https://docs.google.com/spreadsheets/d/x';
    const historical: TeamDto = {
      teamName: 'Relentless', wins: 9, losses: 1, tbd: 0, season: seasonInfo(2, { isHistorical: true, historicalSourceUrl: url }),
      recordFromSource: true, titles: ['Pro'],
      players: [
        { teamPlayerID: 5, userID: 30, gamertag: 'Captain R', draftCaptainOrder: null, draftRound: null, isCaptain: true },
        { teamPlayerID: 6, userID: 31, gamertag: 'Runner', draftCaptainOrder: null, draftRound: null, isCaptain: false },
      ],
    };

    it('shows the banner, the record from the spreadsheet, its titles and captain, and no draft rounds', () => {
      load(historical, []);
      expect(el().querySelector('app-backfilled-banner a')!.getAttribute('href')).toBe(url);
      expect(el().textContent).toContain('From the source spreadsheet');
      expect(el().textContent).toContain('Pro champions');
      const roster = Array.from(el().querySelectorAll('.team-player')).map(p => Array.from(p.children).map(c => c.textContent!.trim()).join(' '));
      expect(roster).toEqual(['C Captain R Captain', 'R Runner']);
    });

    it("shows its players' stats under a heading when it has some", () => {
      load(historical, []);
      stats().hasRows.set(true);
      fixture.detectChanges();
      expect(el().querySelector('#team-stats')).not.toBeNull();
      expect(el().querySelector('app-season-player-stats')!.classList).toContain('team-stats--shown');
    });

    it('says the matches were not recorded, with the spreadsheet, in place of an empty table', () => {
      load(historical, []);
      expect(el().querySelector('app-data-notice')!.textContent).toContain("Match results weren't recorded");
      expect(el().querySelector('app-data-notice a')!.getAttribute('href')).toBe(url);
      expect(el().querySelector('table[mat-table]')).toBeNull();
    });

    it('shows the matches it does have as usual', () => {
      load(historical, [match({})]);
      expect(el().querySelector('app-data-notice')).toBeNull();
      expect(rows().length).toBe(1);
    });
  });
});
