export interface VodTeamDto {
  teamID: number;
  teamName: string;
}

/** A VOD on a season, team, match or player page (GET /api/{seasons|teams|users|season-matches}/{id}/vods). */
export interface VodDto {
  vodID: number;
  youTubeVideoID: string;
  title: string;
  /** ISO 8601, UTC. */
  publishedAt: string;
  durationSeconds?: number | null;
  seasonID?: number | null;
  seasonName?: string | null;
  seasonMatchID?: number | null;
  teams: VodTeamDto[];
}

/** A VOD waiting for a sysadmin (GET /api/vods/review), with the matcher's suggestion in its season, match and teams. */
export interface VodReviewDto extends VodDto {
  description?: string | null;
  publishedAtIsApproximate: boolean;
  matchStatus: string;
  matchNote?: string | null;
}

/** What a VOD is of: a season match (its season and teams follow), or a season and its teams. */
export interface LinkVodRequest {
  seasonID: number | null;
  seasonMatchID: number | null;
  teamIDs: number[];
}

export interface VodLinkSeasonDto {
  seasonID: number;
  seasonName: string;
}

export interface VodLinkMatchDto {
  seasonMatchID: number;
  label: string;
  teamIDs: number[];
}

export interface VodLinkOptionsDto {
  teams: VodTeamDto[];
  matches: VodLinkMatchDto[];
}

/** Which page's VODs: a season match, a season, a team or a player. */
export type VodScope = 'seasonMatch' | 'season' | 'team' | 'player';

/** A duration in seconds as YouTube shows it: "1:50:01", "58:20". */
export function vodDuration(seconds: number | null | undefined): string | null {
  if (seconds == null)
    return null;
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  const two = (n: number) => n.toString().padStart(2, '0');
  return h > 0 ? `${h}:${two(m)}:${two(s)}` : `${m}:${two(s)}`;
}

/** A YouTube video ID: 11 letters, digits, - or _. Only those are put in an embed URL. */
export function isVideoID(id: string): boolean {
  return /^[\w-]{11}$/.test(id);
}
