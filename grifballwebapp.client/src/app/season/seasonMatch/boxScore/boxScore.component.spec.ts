import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { SeasonMatchGameParticipantDto } from '../seasonMatchPageDto';
import { BoxScoreComponent, boxScoreTotals, diff, kdr } from './boxScore.component';

const line = (overrides: Partial<SeasonMatchGameParticipantDto> = {}): SeasonMatchGameParticipantDto => ({
  userID: 1, gamertag: 'Chief', kills: 10, deaths: 5, assists: 2, bombScores: 1, score: null, ...overrides,
});

describe('box score sums', () => {
  it('works out K/D to two places, and the kills when nobody died', () => {
    expect(kdr(10, 5)).toBe('2.00');
    expect(kdr(5, 0)).toBe('5');
    expect(kdr(0, 0)).toBe('0');
  });

  it('signs the kill difference', () => {
    expect(diff(10, 5)).toBe('+5');
    expect(diff(5, 10)).toBe('-5');
    expect(diff(5, 5)).toBe('0');
  });

  it('totals a side, with no score when nobody has one', () => {
    expect(boxScoreTotals([line({ kills: 12, deaths: 0, assists: 1, bombScores: 2 }), line({ kills: 3, deaths: 0, assists: 2, bombScores: 1 })]))
      .toEqual({ kills: 15, deaths: 0, assists: 3, bombScores: 3, diff: 15, kdr: '15', score: null });
    expect(boxScoreTotals([line({ kills: 6, deaths: 4, score: 120 }), line({ kills: 0, deaths: 0, score: null })]))
      .toEqual(jasmine.objectContaining({ kdr: '1.50', score: 120 }));
  });
});

describe('BoxScoreComponent', () => {
  let fixture: ComponentFixture<BoxScoreComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const header = () => el().querySelector('.scoreboard-team-header')!.textContent!.replace(/\s+/g, ' ').trim();
  const cells = (row: Element) => Array.from(row.children).map(c => c.textContent!.trim());

  function create(side: 'home' | 'away', players: SeasonMatchGameParticipantDto[], teamName: string | null = 'Red Rampage', score: number | null = 5): void {
    fixture = TestBed.createComponent(BoxScoreComponent);
    fixture.componentRef.setInput('side', side);
    fixture.componentRef.setInput('teamName', teamName);
    fixture.componentRef.setInput('score', score);
    fixture.componentRef.setInput('players', players);
    fixture.componentRef.setInput('heading', 'Game 1');
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [BoxScoreComponent], providers: [provideRouter([])] }).compileComponents();
  });

  it('heads the home side as home, in the home colours, with its team and score', () => {
    create('home', [line()]);
    expect(header()).toBe('Home Red Rampage 5');
    expect(el().querySelector('.scoreboard-team--home')).not.toBeNull();
    expect(el().querySelector('.hud-table-wrap')!.getAttribute('aria-label')).toBe('Red Rampage Game 1 stats');
  });

  it('heads the away side as away, naming it when its team is not known and leaving out a missing score', () => {
    create('away', [line()], null, null);
    expect(header()).toBe('Away Away Team');
    expect(el().querySelector('.scoreboard-team--away')).not.toBeNull();
    expect(el().querySelector('.scoreboard-team-score')).toBeNull();
  });

  it('shows each player, linking those with a profile, with the side totals below', () => {
    create('home', [
      line({ userID: 7, gamertag: 'Chief', kills: 25, deaths: 10, bombScores: 3 }),
      line({ userID: null, gamertag: 'Ringer', kills: 4, deaths: 8, bombScores: 0 }),
    ]);
    const rows = Array.from(el().querySelectorAll('tbody tr'));
    expect(rows.map(cells)).toEqual([['Chief', '25', '10', '+15', '2.50', '3'], ['Ringer', '4', '8', '-4', '0.50', '0']]);
    expect(rows[0].querySelector('a')!.getAttribute('href')).toBe('/profile/7');
    expect(rows[1].querySelector('a')).toBeNull();
    expect(rows[0].children[3].classList).toContain('hud-stat-pos');
    expect(rows[1].children[3].classList).toContain('hud-stat-neg');
    expect(cells(el().querySelector('tfoot tr')!)).toEqual(['Team Total', '29', '18', '+11', '1.61', '3']);
  });

  it('adds a points column when a player has a score', () => {
    create('home', [line({ score: 300 }), line({ gamertag: 'NoScore', score: null })]);
    expect(Array.from(el().querySelectorAll('thead th')).map(th => th.textContent!.trim())).toContain('Pts');
    expect(Array.from(el().querySelectorAll('tbody tr')).map(r => r.children[6].textContent!.trim())).toEqual(['300', '-']);
    expect(el().querySelector('tfoot tr')!.children[6].textContent!.trim()).toBe('300');
  });

  it('says when a side has no players, with no totals', () => {
    create('away', []);
    expect(el().querySelector('tbody td')!.textContent).toContain('No individual player stats recorded');
    expect(el().querySelector('tbody td')!.getAttribute('colspan')).toBe('6');
    expect(el().querySelector('tfoot')).toBeNull();
  });

  it('keeps a row for every player, even two with the same gamertag', () => {
    create('home', [line({ userID: null, gamertag: 'Unknown Spartan', kills: 4 }), line({ userID: null, gamertag: 'Unknown Spartan', kills: 2 })]);
    expect(Array.from(el().querySelectorAll('tbody tr')).map(r => r.children[1].textContent!.trim())).toEqual(['4', '2']);
  });
});
