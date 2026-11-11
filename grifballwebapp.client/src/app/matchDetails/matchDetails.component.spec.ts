import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatchDetailsComponent, teamName } from './matchDetails.component';
import { formatSeconds, fromApiTimeSpan, parseMatchDetails } from './matchDetailsDto';

const MATCH_ID = '0f8fad5b-d9cb-469f-a165-70867728950e';
const URL = '/api/matches/' + MATCH_ID;

/** A player line as the API sends it. */
const player = (gamertag: string, overrides: Record<string, unknown> = {}) => ({
  userID: 7, gamertag, goals: 3, personalScore: 1200, rank: 1, kills: 12, deaths: 5, assists: 4, kda: 8.333, suicides: 1,
  betrayals: 0, meleeKills: 9, powerWeaponKills: 2, maxKillingSpree: 4, calloutAssists: 1, shotsFired: 30, shotsHit: 15,
  accuracy: 50, damageDealt: 2000, damageTaken: 1500, averageLife: '00:00:45.5000000', timePlayed: '00:10:00', spawns: 6,
  objectivesCompleted: 0, joinedInProgress: false, leftInProgress: false,
  medals: [{ medalID: 1, name: 'Grand Slam', description: 'Score a goal', type: 'Mode', difficulty: 'Normal', count: 2 }],
  ...overrides,
});

/** The match as the API sends it. */
const apiMatch = (overrides: Record<string, unknown> = {}) => ({
  matchID: MATCH_ID,
  startTime: '2026-10-30T23:05:00Z',
  endTime: '2026-10-30T23:15:00Z',
  duration: '00:10:00',
  seasonMatch: { seasonMatchID: 31, seasonID: 12, seasonName: 'Autumn Brawl', gameNumber: 2, bestOf: 3 },
  teams: [
    { teamID: 0, seasonTeamID: 1, seasonTeamName: 'Red Rampage', score: 5, outcome: 'Won', players: [player('Plasma Pete'), player('Ringer', { userID: null, joinedInProgress: true, leftInProgress: true, medals: [] })] },
    { teamID: 1, seasonTeamID: null, seasonTeamName: null, score: 3, outcome: 'DidNotFinish', players: [] },
  ],
  medals: [{ medalID: 1, name: 'Grand Slam', description: 'Score a goal', type: 'Mode', difficulty: 'Normal', count: 2 }],
  ...overrides,
});

describe('matchDetailsDto', () => {
  it('reads a TimeSpan as seconds', () => {
    expect(fromApiTimeSpan('00:10:00')).toBe(600);
    expect(fromApiTimeSpan('00:00:41.5000000')).toBe(41.5);
    expect(fromApiTimeSpan('1.02:03:04')).toBe(93784);
    expect(fromApiTimeSpan('-00:00:05')).toBe(-5);
    expect(fromApiTimeSpan('nonsense')).toBe(0);
    expect(fromApiTimeSpan(null)).toBe(0);
  });

  it('formats seconds as m:ss, and h:mm:ss from an hour', () => {
    expect(formatSeconds(41.5)).toBe('0:42');
    expect(formatSeconds(600)).toBe('10:00');
    expect(formatSeconds(3725)).toBe('1:02:05');
    expect(formatSeconds(-3)).toBe('0:00');
  });

  it('parses the dates and durations, and keeps null', () => {
    const parsed = parseMatchDetails(apiMatch())!;
    expect(parsed.startTime?.toUTC().toISO()).toBe('2026-10-30T23:05:00.000Z');
    expect(parsed.duration).toBe(600);
    expect(parsed.teams[0].players[0].averageLife).toBe(45.5);
    expect(parsed.teams[0].players[0].timePlayed).toBe(600);
    expect(parseMatchDetails(null)).toBeNull();
  });

  it('tolerates missing lists', () => {
    const parsed = parseMatchDetails({ ...apiMatch(), startTime: null, teams: [{ teamID: 0 }], medals: undefined })!;
    expect(parsed.startTime).toBeNull();
    expect(parsed.teams[0].players).toEqual([]);
    expect(parsed.medals).toEqual([]);
    expect(parseMatchDetails({ ...apiMatch(), teams: undefined })!.teams).toEqual([]);
  });

  it('names a team after its season team, or Infinite\'s side', () => {
    expect(teamName({ teamID: 0, seasonTeamName: 'Red Rampage' } as any)).toBe('Red Rampage');
    expect(teamName({ teamID: 1, seasonTeamName: null } as any)).toBe('Team 2');
  });
});

describe('MatchDetailsComponent', () => {
  let fixture: ComponentFixture<MatchDetailsComponent>;
  let httpMock: HttpTestingController;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MatchDetailsComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(MatchDetailsComponent);
    fixture.componentRef.setInput('matchID', MATCH_ID);
    httpMock = TestBed.inject(HttpTestingController);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('says it is loading until the match arrives', () => {
    httpMock.expectOne(URL);
    expect(fixture.componentInstance.state()).toBe('loading');
    expect(el.textContent).toContain('Loading the match');
  });

  it('shows the score, every player\'s stats and the medals', async () => {
    httpMock.expectOne(URL).flush(apiMatch());
    await fixture.whenStable();
    fixture.detectChanges();

    const text = el.textContent!;
    expect(fixture.componentInstance.state()).toBe('loaded');
    expect(text).toContain('Autumn Brawl · Game 2 of best of 3');
    expect(text).toContain('Red Rampage');
    expect(text).toContain('Team 2');
    expect(text).toContain('Did not finish');
    expect(text).toContain('Length 10:00');
    expect(text).toContain('Joined late');
    expect(text).toContain('Left early');
    expect(text).toContain('No players recorded for this team.');
    expect(text).toContain('8.3');
    expect(text).toContain('15 / 30');
    expect(text).toContain('0:46');

    const kicker = el.querySelector<HTMLAnchorElement>('.md-kicker')!;
    expect(kicker.getAttribute('href')).toBe('/seasonmatch/31');
    const links = Array.from(el.querySelectorAll<HTMLAnchorElement>('a')).map(a => a.getAttribute('href'));
    expect(links).toContain('/season/12/team/1');
    expect(links).toContain('/profile/7');

    // The ringer has no profile to link to
    const rows = Array.from(el.querySelectorAll('tbody tr'));
    expect(rows[1].querySelector('a')).toBeNull();
    expect(rows[1].textContent).toContain('Ringer');

    expect(el.querySelectorAll('.md-medal').length).toBe(2); // the match's, and Plasma Pete's
    expect(el.querySelector('.md-player')!.textContent).toContain('Plasma Pete');
    expect(el.querySelector('.md-player')!.textContent).toContain('×2');
  });

  it('shows – for goals and points a custom mode packed and no one could read (#217)', async () => {
    httpMock.expectOne(URL).flush(apiMatch({
      teams: [{ teamID: 0, seasonTeamID: 1, seasonTeamName: 'Red Rampage', score: 5, outcome: 'Won', players: [player('Packed', { goals: null, personalScore: null })] }],
    }));
    await fixture.whenStable();
    fixture.detectChanges();

    const cells = Array.from(el.querySelectorAll('tbody tr')[0].querySelectorAll('td.hud-num')).map(td => td.textContent!.trim());
    expect(cells[0]).toBe('–');
    expect(cells.filter(c => c === '–').length).toBe(2);
  });

  it('says when no medals were earned and the game was not reported', async () => {
    httpMock.expectOne(URL).flush(apiMatch({ seasonMatch: null, startTime: null, medals: [] }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el.textContent).toContain('Halo Infinite match');
    expect(el.textContent).toContain('No medals were earned in this match.');
    expect(el.textContent).not.toContain('Played ');
    expect(el.querySelector('a[href^="/season/"]')).toBeNull();
  });

  it('says the match has not been pulled on a 404', async () => {
    httpMock.expectOne(URL).flush(null, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('notFound');
    expect(el.textContent).toContain('has not been pulled');
  });

  it('says the match has not been pulled when the API answers with nothing', async () => {
    httpMock.expectOne(URL).flush(null);
    await fixture.whenStable();

    expect(fixture.componentInstance.state()).toBe('notFound');
  });

  it('says the request failed on a server error', async () => {
    httpMock.expectOne(URL).flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('error');
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Could not load the match');
  });

  it('loads again when the match ID changes', async () => {
    httpMock.expectOne(URL).flush(apiMatch());
    await fixture.whenStable();

    fixture.componentRef.setInput('matchID', 'other');
    fixture.detectChanges();
    await fixture.whenStable();

    httpMock.expectOne('/api/matches/other');
    expect(fixture.componentInstance.state()).toBe('loading');
    expect(fixture.componentInstance.details()).toBeNull();
  });

  it('cancels the previous match\'s request when the match ID changes before it answers', async () => {
    const first = httpMock.expectOne(URL);

    fixture.componentRef.setInput('matchID', 'other');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(first.cancelled).toBeTrue();
    httpMock.expectOne('/api/matches/other').flush(apiMatch({ matchID: 'other' }));
    await fixture.whenStable();

    expect(fixture.componentInstance.state()).toBe('loaded');
    expect(fixture.componentInstance.details()!.matchID).toBe('other');
  });

  it('cancels the request when the page is left', () => {
    const request = httpMock.expectOne(URL);

    fixture.destroy();

    expect(request.cancelled).toBeTrue();
  });
});
