/**
 * What a page shows, by name: the server's LiveTopics (GrifballWebApp.Application/Live/LiveTopics.cs), spelt the same.
 * A page watches the topics of what it shows (liveReload) and loads it again when one changes.
 */
export const LiveTopics = {
  /** The seasons: their list, the home page's events, and what a season's matches add to it. */
  seasons: 'seasons',
  /** A season's overview tabs: its summary, standings, schedule and this week, its players' stats. */
  season: (seasonID: number) => `season:${seasonID}`,
  /** A season's playoff bracket. */
  bracket: (seasonID: number) => `bracket:${seasonID}`,
  /** A season's teams page: its teams with their rosters and records, and the season's banner. */
  teams: (seasonID: number) => `teams:${seasonID}`,
  /** A team's page: its roster, record, matches and players' stats. */
  team: (teamID: number) => `team:${teamID}`,
  /** A season's draft: its teams, captains and their lock, its player pool. */
  draft: (seasonID: number) => `draft:${seasonID}`,
  /** A season's signups (the staff's signups and grades pages). */
  signups: (seasonID: number) => `signups:${seasonID}`,
  /** A season match's page. */
  seasonMatch: (seasonMatchID: number) => `seasonmatch:${seasonMatchID}`,
  /** A Halo Infinite match's page (its ID as a GUID, as the server writes it: lower case, with hyphens). */
  haloMatch: (matchID: string) => `match:${matchID.toLowerCase()}`,
  /** A player's profile. */
  player: (userID: number) => `player:${userID}`,
  /** An Xbox account's page, and the profile of whoever plays as it. */
  xbox: (xboxUserID: number | string) => `xbox:${xboxUserID}`,
  /** Late League: its seasons, standings, ladders and matches. */
  lateLeague: 'lateleague',
  /** The matchmaking queue's size. */
  queue: 'queue',
  /** The viewer's own place in the queue: sent to them alone, never watched. */
  myQueue: 'queue:me',
  staffUsers: 'staff:users',
  staffClaims: 'staff:claims',
  staffMerges: 'staff:merges',
  staffImports: 'staff:imports',
  staffVods: 'staff:vods',
  staffReschedules: 'staff:reschedules',
  staffStatFlags: 'staff:statflags',
} as const;

/** A season's page's topic: none for no season (0, or not known yet). */
export function seasonTopics(seasonID: number | null | undefined): string[] {
  return seasonID ? [LiveTopics.season(seasonID)] : [];
}
