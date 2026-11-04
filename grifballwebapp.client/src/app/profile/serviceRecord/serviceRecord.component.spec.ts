import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ServiceRecordComponent, ServiceRecordDto, ServiceRecordTotalsDto } from './serviceRecord.component';

function totals(changes: Partial<ServiceRecordTotalsDto> = {}): ServiceRecordTotalsDto {
  return { gamesPlayed: 0, wins: 0, losses: 0, winPercent: null, kills: 0, deaths: 0, kdRatio: null, bombScores: 0, uncountedBombScoreGames: 0, ...changes };
}

function record(changes: Partial<ServiceRecordDto> = {}): ServiceRecordDto {
  return {
    mmr: 1234,
    all: totals({ gamesPlayed: 30, wins: 20, losses: 10, winPercent: 66.7, kills: 300, deaths: 200, kdRatio: 1.5, bombScores: 42, uncountedBombScoreGames: 3 }),
    league: totals({ gamesPlayed: 20, wins: 15, losses: 5, winPercent: 75, kills: 250, deaths: 100, kdRatio: 2.5, bombScores: 40, uncountedBombScoreGames: 1 }),
    matchmaking: totals({ gamesPlayed: 10, wins: 5, losses: 5, winPercent: 50, kills: 50, deaths: 100, kdRatio: 0.5, bombScores: 2 }),
    ...changes,
  };
}

describe('ServiceRecordComponent', () => {
  let fixture: ComponentFixture<ServiceRecordComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ServiceRecordComponent] });
    fixture = TestBed.createComponent(ServiceRecordComponent);
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const stat = (label: string) => Array.from(el().querySelectorAll('.sr-stat')).find(s => s.querySelector('dt')!.textContent!.trim() === label)!;
  const value = (label: string) => stat(label).querySelector('.sr-val')!.textContent!.trim();
  const sub = (label: string) => stat(label).querySelector('.sr-sub')!.textContent!.trim();
  const tab = (text: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('button.hud-tab')).find(b => b.textContent!.trim() === text)!;

  async function show(r: ServiceRecordDto | null): Promise<void> {
    fixture.componentRef.setInput('record', r);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('shows nothing without a record', async () => {
    await show(null);
    expect(el().querySelector('.sr-strip')).toBeNull();
  });

  it('shows K/D, MMR, bomb scores and games over league and Late League together by default', async () => {
    await show(record());
    expect(value('K/D')).toBe('1.50');
    expect(sub('K/D')).toContain('300 K');
    expect(value('MMR')).toBe('1,234');
    expect(sub('MMR')).toBe('Late League');
    expect(value('Bomb scores')).toBe('42');
    expect(sub('Bomb scores')).toBe('3 custom games not counted');
    expect(value('Games')).toBe('30');
    expect(sub('Games')).toBe('20 - 10 · 66.7%');
    expect(tab('All').getAttribute('aria-pressed')).toBe('true');
  });

  it('switches to league or Late League totals', async () => {
    await show(record());
    tab('League').click();
    fixture.detectChanges();
    expect(value('K/D')).toBe('2.50');
    expect(sub('Bomb scores')).toBe('1 custom game not counted');
    expect(tab('League').getAttribute('aria-pressed')).toBe('true');

    tab('Late League').click();
    fixture.detectChanges();
    expect(value('Bomb scores')).toBe('2');
    expect(sub('Bomb scores')).toBe('Goals');
    expect(fixture.componentInstance.scope()).toBe('matchmaking');
  });

  it('shows a dash for no K/D and an unranked player', async () => {
    await show(record({ mmr: null, all: totals() }));
    expect(value('K/D')).toBe('-');
    expect(value('MMR')).toBe('-');
    expect(sub('MMR')).toBe('Unranked');
    expect(sub('Games')).toBe('0 - 0');
  });
});
