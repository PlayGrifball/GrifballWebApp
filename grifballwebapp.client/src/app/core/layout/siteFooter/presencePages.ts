/** The page every staff page is shown as (the server's PresenceSnapshot.StaffArea): not a page to link to. */
export const STAFF_AREA = '/staff';

/** What a page of the site is called, by its route's pattern, the first match winning: the who's online list's names. */
const LABELS: readonly (readonly [RegExp, string])[] = [
  [/^\/$/, 'Home'],
  [/^\/staff$/, 'Staff area'],
  [/^\/seasons$/, 'Seasons'],
  [/^\/season\/\d+\/draft$/i, 'A draft'],
  [/^\/season\/\d+\/teams$/i, 'A season’s teams'],
  [/^\/season\/\d+\/team\/\d+$/i, 'A team'],
  [/^\/season\/\d+\/signupForm$/i, 'Signing up'],
  [/^\/season(\/\d+)?$/i, 'A season'],
  [/^\/seasonAvailability\/\d+$/i, 'Availability'],
  [/^\/seasonmatch\/\d+$/i, 'A match'],
  [/^\/reschedule-request\/\d+$/i, 'A reschedule'],
  [/^\/match\/[\w-]+$/i, 'A game'],
  [/^\/history\/team\/\d+$/i, 'A team’s history'],
  [/^\/profile\/\d+$/i, 'A profile'],
  [/^\/xbox\/\d+$/i, 'An Xbox account'],
  [/^\/leaderboards$/i, 'Leaderboards'],
  [/^\/late-league(\/\d+)?$/i, 'Late League'],
  [/^\/chat$/i, 'Chat'],
  [/^\/(login|reset-password)$/i, 'Signing in'],
  [/^\/register$/i, 'Signing up for an account'],
];

/** What the page at `path` is called; a route the list doesn't know is "Elsewhere". */
export function presenceLabel(path: string): string {
  return LABELS.find(([pattern]) => pattern.test(path))?.[1] ?? 'Elsewhere';
}

/** "1 member", "3 guests": a count and its noun. */
export function counted(count: number, noun: string): string {
  return `${count} ${noun}${count === 1 ? '' : 's'}`;
}
