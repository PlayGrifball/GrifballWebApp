import type { SeasonPlayerStatDto } from '@api/models';
import { combinePhases } from './combinePhases';

function stat(changes: Partial<SeasonPlayerStatDto> = {}): SeasonPlayerStatDto {
  return {
    playerSeasonStatID: 1, userID: 101, gamertag: 'Alpha', teamID: 40, teamName: 'Relentless', stintIndex: 1, isPlayoffs: false,
    isCombined: false, source: 'Matches', gamesPlayed: 10, wins: 7, losses: 3, goals: 5, kills: 50, deaths: 25, assists: 4,
    kdRatio: 2, kdSpread: 25, carrierKills: 6, sprees: 1, multiKills: 2, exterminations: 0, punches: null, roleNote: null, swordKills: null, tradeNote: null, winPercent: 70,
    grabs: null, battingAverage: null, scoringAverage: null, combinedAverage: null, ...changes,
  };
}

describe('combinePhases', () => {
  it("adds a player's regular season and playoffs together, recounting the ratios", () => {
    const [line, ...rest] = combinePhases([
      stat({ punches: 4 }),
      stat({ isPlayoffs: true, gamesPlayed: 2, wins: 1, losses: 1, goals: 1, kills: 8, deaths: 9, assists: 1, carrierKills: 2, punches: null, tradeNote: 'From Blue' }),
    ]);
    expect(rest).toEqual([]);
    expect(line).toEqual(jasmine.objectContaining({
      userID: 101, teamID: 40, gamesPlayed: 12, wins: 8, losses: 4, winPercent: 66.7, goals: 6, kills: 58, deaths: 34,
      kdRatio: 1.71, kdSpread: 24, assists: 5, carrierKills: 8, punches: 4, swordKills: null, tradeNote: 'From Blue',
      playerSeasonStatID: null,
    }));
  });

  it("leaves grifball.com's averages out of a combined line, and keeps a single row as it is", () => {
    const single = stat({ userID: 102, gamertag: 'Bravo', battingAverage: 60, scoringAverage: 70, combinedAverage: 65, grabs: 10 });
    const lines = combinePhases([
      single,
      stat({ battingAverage: 60, grabs: 10 }),
      stat({ isPlayoffs: true, battingAverage: 50, grabs: 3 }),
    ]);
    expect(lines.find(l => l.userID === 102)).toBe(single);
    expect(lines.find(l => l.userID === 101)).toEqual(jasmine.objectContaining({ grabs: 13, battingAverage: null, scoringAverage: null, combinedAverage: null }));
  });

  it('keeps a traded player apart by team, most goals first', () => {
    const lines = combinePhases([
      stat({ teamID: 40, goals: 2 }),
      stat({ teamID: 41, teamName: 'Blue', stintIndex: 2, goals: 3 }),
      stat({ teamID: 41, teamName: 'Blue', isPlayoffs: true, goals: 4 }),
    ]);
    expect(lines.map(l => [l.teamID, l.goals])).toEqual([[41, 7], [40, 2]]);
  });
});
