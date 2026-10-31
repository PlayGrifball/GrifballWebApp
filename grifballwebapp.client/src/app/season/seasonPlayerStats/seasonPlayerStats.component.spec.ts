import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { SeasonPlayerStatsComponent } from './seasonPlayerStats.component';
import { SeasonPlayerStatDto } from '../../api/dtos/seasonPlayerStatDto';

function stat(id: number, gamertag: string, changes: Partial<SeasonPlayerStatDto> = {}): SeasonPlayerStatDto {
  return {
    playerSeasonStatID: id, userID: id + 100, gamertag, teamID: 40, teamName: 'Relentless', stintIndex: 1, isPlayoffs: false,
    isCombined: false, source: 'Manual', gamesPlayed: 10, wins: 7, losses: 3, goals: 5, kills: 50, deaths: 25, assists: 4,
    kdRatio: 2, kdSpread: 25, carrierKills: 6, sprees: 1, multiKills: 2, exterminations: 0, ...changes,
  };
}

const url = (seasonID: number) => '/api/Season/GetSeasonPlayerStats/' + seasonID;

describe('SeasonPlayerStatsComponent', () => {
  let fixture: ComponentFixture<SeasonPlayerStatsComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonPlayerStatsComponent);
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const players = () => Array.from(el().querySelectorAll('tbody tr')).map(r => r.querySelector('td a')!.textContent!.trim());
  const headers = () => Array.from(el().querySelectorAll('thead th')).map(th => th.textContent!.trim());

  /** Asks for the stats of a season (and team), answers with these and shows them. */
  async function load(seasonID: number, rows: SeasonPlayerStatDto[], teamID?: number): Promise<TestRequest> {
    fixture.componentRef.setInput('seasonID', seasonID);
    if (teamID !== undefined)
      fixture.componentRef.setInput('teamID', teamID);
    fixture.detectChanges();
    const req = http.expectOne(r => r.url === url(seasonID));
    req.flush(rows);
    await fixture.whenStable();
    fixture.detectChanges();
    return req;
  }

  describe("a season's stats", () => {
    beforeEach(async () => {
      const req = await load(12, [
        stat(1, 'Alpha', { tradeNote: 'Traded wk 3', goals: 5, kills: 50, gamesPlayed: 10 }),
        stat(2, 'Bravo', { teamID: null, teamName: null, kdRatio: null, goals: 9, kills: 30, gamesPlayed: 12 }),
        stat(3, 'Alpha', { isPlayoffs: true, teamName: 'Relentless', goals: 1, kills: 70, gamesPlayed: 3 }),
      ]);
      expect(req.request.params.has('teamID')).toBeFalse();
    });

    it('lists every row, linking players and their teams', () => {
      expect(players()).toEqual(['Alpha', 'Bravo', 'Alpha']);
      expect(headers()[1]).toBe('Team');
      const first = el().querySelectorAll('tbody tr')[0];
      expect(Array.from(first.querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual(['/profile/101', '/season/12/team/40']);
      expect(first.textContent).toContain('Traded wk 3');
      const second = el().querySelectorAll('tbody tr')[1];
      expect(second.textContent).toContain('No team');
      expect(second.textContent).toContain('-');
    });

    it('filters by phase', () => {
      const buttons = Array.from(el().querySelectorAll<HTMLButtonElement>('.stats-controls button'));
      buttons[2].click();
      fixture.detectChanges();
      expect(players()).toEqual(['Alpha']);
      expect(buttons[2].getAttribute('aria-pressed')).toBe('true');
      buttons[1].click();
      fixture.detectChanges();
      expect(players()).toEqual(['Alpha', 'Bravo']);
    });

    it('filters by gamertag or team, and says when nobody matches', () => {
      const input = el().querySelector<HTMLInputElement>('input')!;
      input.value = 'brav';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(players()).toEqual(['Bravo']);
      input.value = 'relent';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(players()).toEqual(['Alpha', 'Alpha']);
      input.value = 'nobody';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(players()).toEqual([]);
      expect(el().textContent).toContain('No players match.');
    });

    it('sorts by goals, kills or games played from the headers, most first, and flips on a second click', () => {
      const header = (name: string) => Array.from(el().querySelectorAll('thead th')).find(th => th.querySelector('button')?.textContent!.trim().startsWith(name))!;
      const click = (name: string) => {
        header(name).querySelector('button')!.click();
        fixture.detectChanges();
      };
      expect(header('Goals').getAttribute('aria-sort')).toBe('none');

      click('Goals');
      expect(players()).toEqual(['Bravo', 'Alpha', 'Alpha']);
      expect(header('Goals').getAttribute('aria-sort')).toBe('descending');
      expect(header('Goals').querySelector('[aria-hidden="true"]')!.textContent).toBe('▼');

      click('Goals');
      expect(players()).toEqual(['Alpha', 'Alpha', 'Bravo']);
      expect(header('Goals').getAttribute('aria-sort')).toBe('ascending');

      click('K');
      expect(fixture.componentInstance.shown().map(p => p.kills)).toEqual([70, 50, 30]);
      expect(header('K').getAttribute('aria-sort')).toBe('descending');
      expect(header('Goals').getAttribute('aria-sort')).toBe('none');

      click('GP');
      expect(fixture.componentInstance.shown().map(p => p.gamesPlayed)).toEqual([12, 10, 3]);
    });

    it('sorts only the rows the filters leave', () => {
      fixture.componentInstance.phase.set('regular');
      fixture.componentInstance.sortBy('kills');
      fixture.detectChanges();
      expect(fixture.componentInstance.shown().map(p => p.kills)).toEqual([50, 30]);
    });

    it('starts over, without the last season\'s rows, when the season changes', async () => {
      fixture.componentRef.setInput('seasonID', 13);
      fixture.detectChanges();
      const req = http.expectOne(url(13));
      expect(players()).withContext('nothing stale while the next season loads').toEqual([]);
      expect(fixture.componentInstance.loaded()).toBeFalse();
      req.flush([stat(4, 'Charlie')]);
      await fixture.whenStable();
      fixture.detectChanges();
      expect(players()).toEqual(['Charlie']);
    });
  });

  describe("a team's stats", () => {
    it("asks for the team's rows only and leaves out the team column, keeping trade notes", async () => {
      const req = await load(5, [stat(1, 'Legendary'), stat(2, 'Runner', { isPlayoffs: true, tradeNote: 'From Blue' })], 41);
      expect(req.request.params.get('teamID')).toBe('41');
      expect(players()).toEqual(['Legendary', 'Runner']);
      expect(headers()).not.toContain('Team');
      const runner = el().querySelectorAll('tbody tr')[1];
      expect(runner.querySelectorAll('a').length).toBe(1);
      expect(runner.textContent).toContain('From Blue');
      expect(fixture.componentInstance.hasRows()).toBeTrue();
    });

    it('shows nothing at all when the team has no stats', async () => {
      await load(5, [], 42);
      expect(fixture.componentInstance.hasRows()).toBeFalse();
      expect(el().querySelector('table, input, button')).toBeNull();
      expect(el().textContent!.trim()).toBe('');
    });

    it('shows nothing while they load', () => {
      fixture.componentRef.setInput('seasonID', 5);
      fixture.componentRef.setInput('teamID', 41);
      fixture.detectChanges();
      expect(el().querySelector('table')).toBeNull();
      http.expectOne(r => r.url === url(5)).flush([]);
    });

    it('reloads when the team changes', async () => {
      await load(5, [stat(1, 'Legendary')], 41);
      fixture.componentRef.setInput('teamID', 42);
      fixture.detectChanges();
      http.expectOne(r => r.params.get('teamID') === '42').flush([stat(3, 'Bench Boss')]);
      await fixture.whenStable();
      fixture.detectChanges();
      expect(players()).toEqual(['Bench Boss']);
    });
  });
});

describe('SeasonPlayerStatsComponent failing', () => {
  it('shows an empty table when the stats cannot be loaded', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SeasonPlayerStatsComponent);
    fixture.componentRef.setInput('seasonID', 3);
    fixture.detectChanges();
    http.expectOne(url(3)).flush('', { status: 500, statusText: 'down' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.componentInstance.loaded()).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No players match.');
  });

  it("shows nothing for a team whose stats cannot be loaded", async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SeasonPlayerStatsComponent);
    fixture.componentRef.setInput('seasonID', 3);
    fixture.componentRef.setInput('teamID', 9);
    fixture.detectChanges();
    http.expectOne(r => r.url === url(3)).flush('', { status: 500, statusText: 'down' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('table')).toBeNull();
  });
});
