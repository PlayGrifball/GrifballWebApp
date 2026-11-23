/**
 * Maps a season's year, league and Halo title to its Grifball historical era.
 * Used for archive provenance banners and era context badges without altering layout structure.
 */
export interface GrifballEra {
  id: 'founding' | 'expansion' | 'hub' | 'revival' | 'infinite';
  label: string;
}

export function grifballEra(year: number | null | undefined, gameSlug?: string | null): GrifballEra {
  const known = year != null && year > 0;
  if (known && year <= 2010) {
    return { id: 'founding', label: 'Founding Era (grifball.com)' };
  }
  if (known && year <= 2013) {
    return { id: 'expansion', label: 'Expansion & GGL (grifball.info)' };
  }
  if (known && year <= 2018) {
    return { id: 'hub', label: 'GrifballHub & EUGBL Era' };
  }
  if (gameSlug === 'infinite' || (known && year >= 2023)) {
    return { id: 'infinite', label: 'Halo Infinite & PlayGrifball' };
  }
  return { id: 'revival', label: 'Revival, GWL & Late League' };
}

/** Formats an active year span (e.g. "2013 – 2026" or "2024") from a list of season years. */
export function activeYearSpan(years: readonly (number | null | undefined)[]): string | null {
  const valid = years.filter((y): y is number => y != null && y > 0);
  if (valid.length === 0)
    return null;
  const min = Math.min(...valid);
  const max = Math.max(...valid);
  return min === max ? `${min}` : `${min} – ${max}`;
}
