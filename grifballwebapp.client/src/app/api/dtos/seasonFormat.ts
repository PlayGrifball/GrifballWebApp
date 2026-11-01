/** What kind of competition a season is (the server's SeasonFormat, sent by name). */
export type SeasonFormat = 'LeagueSeason' | 'Tournament' | 'LateLeague';

/** The formats in the order the editor and filters offer them, with their labels. */
export const seasonFormats: { value: SeasonFormat; label: string }[] = [
  { value: 'LeagueSeason', label: 'League season' },
  { value: 'Tournament', label: 'Tournament' },
  { value: 'LateLeague', label: 'Late League' },
];

/** A format's label; a league season (the default, also for a season without one) is "League season". */
export function seasonFormatLabel(format: SeasonFormat | null | undefined): string {
  return seasonFormats.find(f => f.value === format)?.label ?? 'League season';
}
