import type { SeasonPlayerStatDto } from '@api/models';

const round = (value: number, places: number) => Math.round(value * 10 ** places) / 10 ** places;

/** Null when no row records it, else the total of the rows that do. */
function sumRecorded(rows: SeasonPlayerStatDto[], pick: (p: SeasonPlayerStatDto) => number | null): number | null {
  const recorded = rows.map(pick).filter((v): v is number => v != null);
  return recorded.length ? recorded.reduce((a, b) => a + b, 0) : null;
}

const sum = (rows: SeasonPlayerStatDto[], pick: (p: SeasonPlayerStatDto) => number) => rows.reduce((a, p) => a + pick(p), 0);

/** One player's rows for one team (their regular season and playoffs) as one line. */
function combine(rows: SeasonPlayerStatDto[]): SeasonPlayerStatDto {
  if (rows.length === 1)
    return rows[0];
  const [first] = rows;
  const gamesPlayed = sum(rows, p => p.gamesPlayed);
  const wins = sum(rows, p => p.wins);
  const kills = sum(rows, p => p.kills);
  const deaths = sum(rows, p => p.deaths);
  return {
    ...first,
    playerSeasonStatID: null,
    isPlayoffs: false,
    isCombined: true,
    tradeNote: rows.find(p => p.tradeNote)?.tradeNote ?? null,
    roleNote: rows.find(p => p.roleNote)?.roleNote ?? null,
    gamesPlayed,
    wins,
    losses: sum(rows, p => p.losses),
    // As the server's StatMath counts them
    winPercent: gamesPlayed > 0 ? round(100 * wins / gamesPlayed, 1) : null,
    kills,
    deaths,
    kdRatio: deaths > 0 ? round(kills / deaths, 2) : null,
    kdSpread: kills - deaths,
    goals: sum(rows, p => p.goals),
    assists: sum(rows, p => p.assists),
    carrierKills: sum(rows, p => p.carrierKills),
    sprees: sum(rows, p => p.sprees),
    multiKills: sum(rows, p => p.multiKills),
    exterminations: sum(rows, p => p.exterminations),
    punches: sumRecorded(rows, p => p.punches),
    swordKills: sumRecorded(rows, p => p.swordKills),
    grabs: sumRecorded(rows, p => p.grabs),
    // grifball.com's averages need betrayals the rows don't carry: not recounted
    battingAverage: null,
    scoringAverage: null,
    combinedAverage: null,
  };
}

/**
 * A player's regular season and playoffs as one line per team they played for, most goals first (then kills, then
 * gamertag), as the server orders a phase.
 */
export function combinePhases(rows: SeasonPlayerStatDto[]): SeasonPlayerStatDto[] {
  const groups = new Map<string, SeasonPlayerStatDto[]>();
  for (const p of rows) {
    const key = `${p.userID}-${p.teamID}`;
    const group = groups.get(key);
    if (group)
      group.push(p);
    else
      groups.set(key, [p]);
  }
  return [...groups.values()].map(combine)
    .sort((a, b) => b.goals - a.goals || b.kills - a.kills || a.gamertag.localeCompare(b.gamertag));
}
