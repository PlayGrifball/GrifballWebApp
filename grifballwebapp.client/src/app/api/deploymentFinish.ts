import type { DeploymentFinish, DeploymentHistoryDto } from '@api/models';

const FINISH_LABELS: Record<DeploymentFinish, string> = {
  Champion: 'Champion',
  Playoffs: 'Playoffs',
  Eliminated: 'Eliminated',
  RegularSeason: 'Regular season',
  InProgress: 'In progress',
};

/** A finish as the profile shows it: a champion's with each tier it won, as the season pages name them ("Open Champion", "Amateur & Pro Champion"). */
export function deploymentFinishLabel(line: Pick<DeploymentHistoryDto, 'finish' | 'championshipTiers'>): string {
  // A finish this client does not know yet (a newer server) shows by its name
  const label = FINISH_LABELS[line.finish] ?? line.finish;
  const tiers = line.championshipTiers;
  return line.finish === 'Champion' && tiers.length ? `${tiers.join(' & ')} ${label}` : label;
}
