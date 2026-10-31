import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LeaderboardsComponent } from './leaderboards.component';
import { LeaderboardsDto } from '../api/dtos/leaderboardsDto';

describe('LeaderboardsComponent', () => {
  let fixture: ComponentFixture<LeaderboardsComponent>;
  let http: HttpTestingController;
  const leader = (userID: number, gamertag: string, statValue: number, perGame: number | null = 1.5) =>
    ({ userID, gamertag, statValue, gamesPlayed: 30, perGame, seasonsCount: 3 });
  const boards: LeaderboardsDto = {
    ringLeaders: [{ userID: 1, gamertag: 'Legendary', ringsCount: 5, titles: ['Summer League 2011 (Pro)', 'Winter League 2012 (Pro)'] }],
    dynasties: [{ teamName: 'Relentless', latestTeamID: 40, latestSeasonID: 10, championshipsCount: 4, seasonsActive: 6, titles: ['Summer League 2011 (Pro)'] }],
    goalLeaders: [leader(1, 'Legendary', 120)],
    killLeaders: [leader(2, 'Killer', 500)],
    gamesPlayedLeaders: [leader(3, 'Ironman', 300, null)],
    carrierStopLeaders: [leader(4, 'Wall', 45)],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LeaderboardsComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('tbody tr')).map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));
  const choose = (title: string) => {
    Array.from(el().querySelectorAll<HTMLButtonElement>('.hud-tab')).find(b => b.textContent!.trim() === title)!.click();
    fixture.detectChanges();
  };

  it('shows each board, linking players to their profiles and teams to their latest title', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();

    expect(rows()).toEqual([['1', 'Legendary', '5', 'Summer League 2011 (Pro), Winter League 2012 (Pro)']]);
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/profile/1');

    choose('Dynasties');
    expect(rows()[0].slice(0, 4)).toEqual(['1', 'Relentless', '4', '6']);
    expect(el().querySelector('tbody a')!.getAttribute('href')).toBe('/season/10/team/40');

    choose('Kills');
    expect(rows()).toEqual([['1', 'Killer', '500', '30', '1.50', '3']]);
    choose('Goals');
    expect(rows()[0][1]).toBe('Legendary');
    choose('Carrier stops');
    expect(rows()[0][1]).toBe('Wall');
    choose('Games played');
    expect(rows()).toEqual([['1', 'Ironman', '300', '30', '-', '3']]);
    expect(el().querySelector('[aria-pressed="true"]')!.textContent!.trim()).toBe('Games played');
  });

  it('marks the chosen board pressed, names the table after it and scopes every header to its column', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush(boards);
    fixture.detectChanges();
    const pressed = () => Array.from(el().querySelectorAll('.hud-tab')).map(b => b.getAttribute('aria-pressed'));
    expect(pressed()).toEqual(['true', 'false', 'false', 'false', 'false', 'false']);
    expect(el().querySelector('.hud-table-wrap')!.getAttribute('aria-label')).toBe('Championships leaderboard');

    for (const title of ['Championships', 'Dynasties', 'Goals']) {
      choose(title);
      const headers = Array.from(el().querySelectorAll('th'));
      expect(headers.length).toBeGreaterThan(0);
      expect(headers.every(th => th.getAttribute('scope') === 'col')).withContext(title).toBeTrue();
    }
    expect(pressed()).toEqual(['false', 'false', 'true', 'false', 'false', 'false']);
    expect(el().querySelector('.hud-table-wrap')!.getAttribute('aria-label')).toBe('Goals leaderboard');
    expect(fixture.componentInstance.metric()!.leaders[0].perGameText).toBe('1.50');
  });

  it('says when the leaderboards cannot be loaded', () => {
    http.expectOne('/api/Leaderboards/GetLeaderboards').flush('', { status: 500, statusText: 'down' });
    fixture.detectChanges();
    expect(el().textContent).toContain("couldn't be loaded");
  });
});
