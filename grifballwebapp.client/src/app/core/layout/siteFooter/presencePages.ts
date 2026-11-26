/** The page every staff page is shown as (the server's PresenceSnapshot.StaffArea): not a page to link to. */
export const STAFF_AREA = '/staff';

/** A page's name: without what it is about, and (for a page about something) with its name (PresencePage.subject). */
type Label = readonly [generic: string, named?: (subject: string) => string];

/** "Grunt Padre’s": a name's possessive. */
const whose = (name: string) => `${name}’s`;

/**
 * What a page of the site is called, by its route's pattern, the first match winning: the who's online list's names,
 * saying whose profile and which season, team or match it is once the server has named it.
 */
const LABELS: readonly (readonly [RegExp, Label])[] = [
  [/^\/$/, ['Home']],
  [/^\/staff$/, ['Staff area']],
  [/^\/seasons$/, ['Seasons']],
  [/^\/season\/\d+\/draft$/i, ['A draft', season => `${season} draft`]],
  [/^\/season\/\d+\/teams$/i, ['A season’s teams', season => `${season} teams`]],
  [/^\/season\/\d+\/team\/\d+$/i, ['A team', team => team]],
  [/^\/season\/\d+\/signupForm$/i, ['Signing up', season => `Signing up for ${season}`]],
  [/^\/season(\/\d+)?$/i, ['A season', season => season]],
  [/^\/seasonAvailability\/\d+$/i, ['Availability', season => `${season} availability`]],
  [/^\/seasonmatch\/\d+$/i, ['A match', match => match]],
  [/^\/reschedule-request\/\d+$/i, ['A reschedule', match => `Rescheduling ${match}`]],
  [/^\/match\/[\w-]+$/i, ['A game', match => `A game of ${match}`]],
  [/^\/history\/team\/\d+$/i, ['A team’s history', team => `History of ${team}`]],
  [/^\/profile\/\d+$/i, ['A profile', player => `${whose(player)} profile`]],
  [/^\/xbox\/\d+$/i, ['An Xbox account', gamertag => `${whose(gamertag)} Xbox account`]],
  [/^\/leaderboards$/i, ['Leaderboards']],
  [/^\/late-league(\/\d+)?$/i, ['Late League', season => `Late League: ${season}`]],
  [/^\/chat$/i, ['Chat']],
  [/^\/(login|reset-password)$/i, ['Signing in']],
  [/^\/register$/i, ['Signing up for an account']],
];

/**
 * What the page at `path` is called, naming its `subject` (whose profile, which season) when the server gave one; a
 * route the list doesn't know is "Elsewhere".
 */
export function presenceLabel(path: string, subject?: string | null): string {
  const label = LABELS.find(([pattern]) => pattern.test(path))?.[1];
  if (!label)
    return 'Elsewhere';
  const [generic, named] = label;
  return subject && named ? named(subject) : generic;
}

/** "1 member", "3 guests": a count and its noun. */
export function counted(count: number, noun: string): string {
  return `${count} ${noun}${count === 1 ? '' : 's'}`;
}
