import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { SeasonPlayerStatDto, SeasonPlayerStatsComponent } from './seasonPlayerStats.component';

function stat(id: number, gamertag: string, changes: Partial<SeasonPlayerStatDto> = {}): SeasonPlayerStatDto {
  return {
    playerSeasonStatID: id, userID: id + 100, gamertag, teamID: 40, teamName: 'Relentless', stintIndex: 1, isPlayoffs: false,
    isCombined: false, source: 'Manual', gamesPlayed: 10, wins: 7, losses: 3, goals: 5, kills: 50, deaths: 25, assists: 4,
    kdRatio: 2, kdSpread: 25, carrierKills: 6, sprees: 1, multiKills: 2, exterminations: 0, ...changes,
  };
}

describe('SeasonPlayerStatsComponent', () => {
  let fixture: ComponentFixture<SeasonPlayerStatsComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonPlayerStatsComponent);
    fixture.componentRef.setInput('seasonID', 12);
    fixture.detectChanges();
    http.expectOne('/api/Season/GetSeasonPlayerStats/12').flush([
      stat(1, 'Alpha', { tradeNote: 'Traded wk 3' }),
      stat(2, 'Bravo', { teamID: null, teamName: null, kdRatio: null }),
      stat(3, 'Alpha', { isPlayoffs: true, teamName: 'Relentless' }),
    ]);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const players = () => Array.from(el().querySelectorAll('tbody tr')).map(r => r.querySelector('td')!.textContent!.trim());

  it('lists every row, linking players and their teams', () => {
    expect(players()).toEqual(['Alpha', 'Bravo', 'Alpha']);
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
});

describe('SeasonPlayerStatsComponent failing', () => {
  it('shows an empty table when the stats cannot be loaded', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SeasonPlayerStatsComponent);
    fixture.componentRef.setInput('seasonID', 3);
    fixture.detectChanges();
    http.expectOne('/api/Season/GetSeasonPlayerStats/3').flush('', { status: 500, statusText: 'down' });
    fixture.detectChanges();
    expect(fixture.componentInstance.loaded()).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No players match.');
  });
});
