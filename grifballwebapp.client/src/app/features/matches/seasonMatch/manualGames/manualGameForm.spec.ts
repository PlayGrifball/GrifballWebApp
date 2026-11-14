import { DateTime } from 'luxon';
import type { GameVersionOptionDto, TeamPagePlayerDto } from '@api/models';
import { ReportedGameDto } from '../seasonMatchPageDto';
import { defaultGameVersion, formLines, gameSource, INFINITE_GAME_VERSION_ID, ManualGameForm, toLocalInput, toRequest } from './manualGameForm';

describe('manualGameForm', () => {
  it('says where a game came from: Waypoint (a match ID) first, else by hand, else recorded', () => {
    const from = (matchID: string | null, isManual: boolean) => gameSource({ matchID, isManual } as ReportedGameDto);
    expect(from('a1b2', false)).toBe('from Waypoint');
    expect(from('a1b2', true)).toBe('from Waypoint');
    expect(from(null, true)).toBe('entered by hand');
    expect(from(null, false)).toBe('recorded');
  });

  const form = (overrides: Partial<ManualGameForm> = {}): ManualGameForm => ({
    gameVersionID: 1, map: '', gametype: ' Grifball ', playedAt: '', homeTeamScore: 5, awayTeamScore: 3, players: [], slot: null, ...overrides,
  });

  it('turns a local time into UTC, and leaves out a bad one', () => {
    const local = '2026-01-10T20:30';
    expect(toRequest(form({ playedAt: local })).playedAt).toBe(DateTime.fromISO(local).toUTC().toISO());
    expect(toRequest(form({ playedAt: 'not a time' })).playedAt).toBeNull();
    expect(toRequest(form()).gametype).toBe('Grifball');
  });

  it('fills blank stats with zero and a blank score with none', () => {
    const blank = null as unknown as number;
    const line = { userID: 1, gamertag: 'Grif', isHomeTeam: true, played: true, kills: blank, deaths: 2, assists: blank, goals: blank, score: null };
    const other = { userID: 2, gamertag: 'Tucker', isHomeTeam: false, played: true, kills: 4, deaths: blank, assists: 1, goals: 1, score: 3 };
    expect(toRequest(form({ homeTeamScore: null, awayTeamScore: null, players: [line, other] }))).toEqual(jasmine.objectContaining({
      homeTeamScore: 0,
      awayTeamScore: 0,
      players: [
        { userID: 1, isHomeTeam: true, kills: 0, deaths: 2, assists: 0, goals: 0, score: null },
        { userID: 2, isHomeTeam: false, kills: 4, deaths: 0, assists: 1, goals: 1, score: 3 },
      ],
    }));
  });

  it('shows no time for none or a bad one', () => {
    expect(toLocalInput(null)).toBe('');
    expect(toLocalInput('garbage')).toBe('');
  });

  it('enters a game as Halo Infinite unless it isn\'t offered: then the first version offered', () => {
    const version = (gameVersionID: number) => ({ gameVersionID, name: `v${gameVersionID}` }) as GameVersionOptionDto;
    expect(defaultGameVersion([])).toBe(INFINITE_GAME_VERSION_ID);
    expect(defaultGameVersion([version(1), version(INFINITE_GAME_VERSION_ID)])).toBe(INFINITE_GAME_VERSION_ID);
    expect(defaultGameVersion([version(3), version(1)])).toBe(3);
  });

  it('gives each roster player a line, filled in from the game edited, and its subs after them', () => {
    const roster = (userID: number, gamertag: string | null) => ({ userID, gamertag }) as TeamPagePlayerDto;
    const game = {
      homePlayers: [{ userID: 1, gamertag: 'Grif', kills: 9, deaths: 3, assists: 1, bombScores: 2, score: 2 }],
      awayPlayers: [
        { userID: 99, gamertag: 'Sub', kills: 1, deaths: 6, assists: 0, bombScores: 0, score: null },
        { userID: null, gamertag: 'Ghost', kills: 0, deaths: 0, assists: 0, bombScores: 0, score: null },
      ],
    } as ReportedGameDto;
    const lines = formLines(game, [roster(1, 'Grif'), roster(2, null)], [roster(3, 'Tucker')]);
    expect(lines.map(l => [l.gamertag, l.isHomeTeam, l.played])).toEqual([
      ['Grif', true, true], ['Unknown Spartan', true, false], ['Tucker', false, false], ['Sub', false, true],
    ]);
    expect(lines[0]).toEqual(jasmine.objectContaining({ kills: 9, goals: 2, score: 2 }));
    expect(formLines(null, [], [roster(3, 'Tucker')]).map(l => l.played)).toEqual([false]);
  });
});
