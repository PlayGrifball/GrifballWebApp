import type { SeasonInfoDto } from '@api/models';

/** A season's facts for specs: an AGLA Halo Infinite season of 2025 played in the app, unless changed. */
export function seasonInfo(seasonID: number, changes: Partial<SeasonInfoDto> = {}): SeasonInfoDto {
  return {
    seasonID, seasonName: `Season ${seasonID}`, start: '2025-01-01T00:00:00Z', end: '2025-03-01T00:00:00Z', year: 2025, hasEnded: true,
    leagueID: 1, leagueName: 'AGLA', leagueSlug: 'agla', gameVersionID: 6, gameVersionName: 'Halo Infinite', gameVersionSlug: 'infinite',
    division: null, isHistorical: false, historicalSourceUrl: null, format: 'LeagueSeason', ...changes,
  };
}
