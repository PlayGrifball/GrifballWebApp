import type { Observable } from 'rxjs';
import type { VodDto } from '@api/models';
import type { VodService } from '@api/services';

/** Which page's VODs: a season match, a season, a team or a player. */
export type VodScope = 'seasonMatch' | 'season' | 'team' | 'player';

/** A page's VODs, newest first: whichever of VodService's per-resource operations its scope needs. */
export function getVods(api: VodService, scope: VodScope, id: number): Observable<VodDto[]> {
  switch (scope) {
    case 'seasonMatch': return api.getSeasonMatchVods({ seasonMatchID: id });
    case 'season': return api.getSeasonVods({ seasonID: id });
    case 'team': return api.getTeamVods({ teamID: id });
    case 'player': return api.getPlayerVods({ userID: id });
  }
}

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
