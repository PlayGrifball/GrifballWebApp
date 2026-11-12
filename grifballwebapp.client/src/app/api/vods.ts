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
