import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PlayerSpotlightComponent } from './player-spotlight.component';
import type { PlayerAllTimeRankDto } from '../../api/dtos/playerAllTimeRankDto';
import type { Board } from '../board';

describe('PlayerSpotlightComponent', () => {
  let fixture: ComponentFixture<PlayerSpotlightComponent>;
  const player: PlayerAllTimeRankDto = {
    userID: 7,
    gamertag: 'Legendary',
    matchedAlias: 'The Silvaa',
    pastGamertags: ['The Silvaa', 'Old Silva'],
    isDummyUser: true,
    rings: { rank: null, totalRanked: 40, statValue: 0, secondaryText: null },
    goals: { rank: 2, totalRanked: 300, statValue: 120, secondaryText: '4 / G in Halo 3' },
    kills: { rank: 10, totalRanked: 250, statValue: 350, secondaryText: '1.75 K/D' },
    gamesPlayed: { rank: 15, totalRanked: 250, statValue: 30, secondaryText: '3 Seasons' },
    carrierStops: { rank: 4, totalRanked: 200, statValue: 20 },
  };

  const render = (shown: PlayerAllTimeRankDto, board: Board = 'goals') => {
    fixture = TestBed.createComponent(PlayerSpotlightComponent);
    fixture.componentRef.setInput('player', shown);
    fixture.componentRef.setInput('board', board);
    fixture.detectChanges();
  };

  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  const el = () => fixture.nativeElement as HTMLElement;
  const tiles = () => Array.from(el().querySelectorAll<HTMLButtonElement>('.hud-grid button'));
  const text = (node: Element) => node.textContent!.replace(/\s+/g, ' ').trim();

  it('shows a tile per board with the rank among those on it, and Unranked with none of the stat', () => {
    render(player);
    expect(tiles().map(t => Array.from(t.children).map(text))).toEqual([
      ['Championships', 'Unranked of 40', '0 Titles'],
      ['Goals', '#2 of 300', '120 Goals (4 / G in Halo 3)'],
      ['Kills', '#10 of 250', '350 Kills (1.75 K/D)'],
      ['Games played', '#15 of 250', '30 Games (3 Seasons)'],
      ['Carrier stops', '#4 of 200', '20 Stops'],
    ]);
  });

  it('names one of a stat in the singular, and shows the title count once', () => {
    const one = { rank: 1, totalRanked: 9, statValue: 1, secondaryText: null };
    render({ ...player, rings: one, goals: one, kills: one, gamesPlayed: { ...one, secondaryText: '1 Season' }, carrierStops: one });
    expect(tiles().map(t => text(t.children[2]))).toEqual(['1 Title', '1 Goal', '1 Kill', '1 Game (1 Season)', '1 Stop']);
  });

  it('names the player, linked to their profile, with the alias matched, their past gamertags and the archive mark', () => {
    render(player);
    expect(el().querySelector('a')!.getAttribute('href')).toBe('/profile/7');
    expect(Array.from(el().querySelectorAll('.spotlight__head .hud-label')).map(text)).toEqual(['Matched via past alias: “The Silvaa”', 'Archive']);
    expect(text(el().querySelector('p')!)).toBe('Also known as: The Silvaa, Old Silva');
    expect(el().querySelector('section')!.getAttribute('aria-label')).toBe('Player ranking spotlight');
  });

  it('leaves out the alias, past gamertags and archive mark when there are none', () => {
    render({ ...player, matchedAlias: null, pastGamertags: [], isDummyUser: false });
    expect(el().querySelectorAll('.spotlight__head .hud-label').length).toBe(0);
    expect(el().querySelector('p')).toBeNull();
  });

  it('makes each tile a button pressed for the board on show, that opens its board', () => {
    render(player, 'kills');
    const chosen: Board[] = [];
    fixture.componentInstance.boardChange.subscribe(b => chosen.push(b));
    expect(tiles().map(t => t.getAttribute('aria-pressed'))).toEqual(['false', 'false', 'true', 'false', 'false']);
    expect(tiles()[2].classList).toContain('hud-card--active');
    expect(tiles().every(t => t.type === 'button')).toBeTrue();

    tiles()[0].click();
    tiles()[4].click();
    expect(chosen).toEqual(['rings', 'stops']);
  });

  it('closes with its close button', () => {
    render(player);
    let closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);
    el().querySelector<HTMLButtonElement>('[aria-label="Clear player standing"]')!.click();
    expect(closed).toBe(1);
  });
});
