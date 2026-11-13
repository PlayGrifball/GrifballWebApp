import type { QuickSearchPlayerDto } from '@api/models';

/**
 * What a player hit says beside the name: the past or other name the search matched, else where the name comes from, so
 * two players of one name are told apart: the gamertag an Xbox account holds now, or an archive player's (with the years
 * they played; whoever holds that gamertag today may be someone else).
 */
export function playerDetail(p: QuickSearchPlayerDto): string | null {
  if (p.pastGamertag)
    return `formerly ${p.pastGamertag}`;
  if (p.matchedName)
    return `aka ${p.matchedName}`;
  if (p.kind === 'Xbox')
    return 'Current gamertag';
  if (p.kind === 'Archive')
    return p.years ? `Archive · ${p.years}` : 'Archive';
  return null;
}
